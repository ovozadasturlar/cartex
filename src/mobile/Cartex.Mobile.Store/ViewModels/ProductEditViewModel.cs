using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProductEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly IProductsApi _products;
    private readonly ICategoriesApi _categories;
    private readonly IFeaturesApi _features;
    private readonly IRatesApi _rates;
    private readonly IUnitsApi _units;
    private readonly IStorageApi _storage;
    private readonly IBarcodesApi _barcodes;
    private readonly ImageUrlBuilder _images;
    private readonly MobilePermissions _permissions;

    private long _variantId;
    private string? _initialBarcode;
    private bool _isCreate;
    private ProductDto? _product;
    private string? _imageKey;

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CurrencyDto> PriceCurrencies { get; } = [];
    public ObservableCollection<ProductBarcodeRow> Barcodes { get; } = [];

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _imageLinkText = "";
    [ObservableProperty] private CategoryDto? _category;
    [ObservableProperty] private CurrencyDto? _priceCurrency;
    [ObservableProperty] private string? _previewUrl;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canManageBarcodes;
    [ObservableProperty] private string _newBarcode = "";
    [ObservableProperty] private string _newBarcodePackQtyText = "1";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _notice;

    public bool CanEdit => _permissions.Has(_isCreate ? "products.create" : "products.edit");
    public bool CanCreateBarcode => _permissions.Has("barcodes.create") && !_isCreate;
    public bool CanDeleteBarcode => _permissions.Has("barcodes.delete") && !_isCreate;
    public bool CanChoosePriceCurrency => PriceCurrencies.Count > 0;

    public ProductEditViewModel(
        IProductsApi products,
        ICategoriesApi categories,
        IFeaturesApi features,
        IRatesApi rates,
        IUnitsApi units,
        IStorageApi storage,
        IBarcodesApi barcodes,
        ImageUrlBuilder images,
        MobilePermissions permissions)
    {
        _products = products;
        _categories = categories;
        _features = features;
        _rates = rates;
        _units = units;
        _storage = storage;
        _barcodes = barcodes;
        _images = images;
        _permissions = permissions;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id))
            _variantId = long.TryParse(Convert.ToString(id), out var parsed) ? parsed : 0;
        if (query.TryGetValue("barcode", out var b))
            _initialBarcode = Convert.ToString(b);
        _isCreate = _variantId == 0;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanCreateBarcode));
        OnPropertyChanged(nameof(CanDeleteBarcode));
        CanManageBarcodes = CanCreateBarcode || CanDeleteBarcode;
    }

    public async Task AppearAsync()
    {
        if (!CanEdit || _product is not null) return;
        await RunAsync(async () =>
        {
            foreach (var c in await _categories.GetAllAsync())
                Categories.Add(c);

            if (!_isCreate)
            {
                _product = (await _products.GetAllAsync(variantId: _variantId)).FirstOrDefault()
                    ?? throw new InvalidOperationException(Loc.Instance["product_not_found"]);
            }

            await LoadPriceCurrenciesAsync();

            if (_isCreate)
            {
                // New product — prefill barcode as code
                Code = _initialBarcode ?? "";
                return;
            }

            var product = _product!;
            Name = product.Name;
            Code = product.Code ?? "";
            PriceText = product.SellingPrice?.ToString("0.##") ?? "";
            _imageKey = product.ImageKey;
            PreviewUrl = _images.FromKey(product.ImageKey);
            Category = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
            await LoadBarcodesAsync();
        });
    }

    private async Task LoadPriceCurrenciesAsync()
    {
        var enabledFeatures = await _features.GetEnabledAsync();
        if (!enabledFeatures.Contains("multicurrency_pricing", StringComparer.OrdinalIgnoreCase))
            return;

        foreach (var currency in await _rates.GetCurrenciesAsync())
        {
            if (currency.IsEnabled || string.Equals(currency.Code, _product?.PriceCurrency, StringComparison.OrdinalIgnoreCase))
                PriceCurrencies.Add(currency);
        }

        PriceCurrency = PriceCurrencies.FirstOrDefault(c =>
                            string.Equals(c.Code, _product?.PriceCurrency, StringComparison.OrdinalIgnoreCase))
                        ?? PriceCurrencies.FirstOrDefault(c => c.IsBase);
        OnPropertyChanged(nameof(CanChoosePriceCurrency));
    }

    [RelayCommand]
    private Task TakePhotoAsync() => RunAsync(async () =>
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            Ui.Toast(Loc.Instance["camera_capture_unavailable"]);
            return;
        }

        // A scanner page may have released the camera only moments before this page opens.
        await Task.Delay(250);
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is not null)
                await UploadImageAsync(photo, deleteTemporaryFileAfterUpload: true);
        }
        catch (PermissionException ex)
        {
            Error = ex.Message;
            Ui.Toast(Error);
        }
    });

    [RelayCommand]
    private Task PickPhotoAsync() => RunAsync(async () =>
    {
        var photo = (await MediaPicker.Default.PickPhotosAsync())?.FirstOrDefault();
        if (photo is not null)
            await UploadImageAsync(photo);
    });

    private async Task UploadImageAsync(FileResult file, bool deleteTemporaryFileAfterUpload = false)
    {
        var uploaded = false;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var result = await _storage.UploadAsync(new Refit.StreamPart(stream, file.FileName, file.ContentType));
            ApplyImage(result);
            uploaded = true;
        }
        finally
        {
            if (uploaded && deleteTemporaryFileAfterUpload)
                DeleteTemporaryCapture(file.FullPath);
        }
    }

    private static void DeleteTemporaryCapture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            return;

        try
        {
            var cacheDirectory = Path.GetFullPath(FileSystem.CacheDirectory);
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(cacheDirectory, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [RelayCommand]
    private Task ApplyLinkAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(ImageLinkText)) return;
        var result = await _storage.UploadFromUrlAsync(new ImageFromUrlRequest(ImageLinkText.Trim()));
        ImageLinkText = "";
        ApplyImage(result);
    });

    private void ApplyImage(UploadResult result)
    {
        _imageKey = result.Key;
        PreviewUrl = _images.FromKey(result.Key);
        Notice = Loc.Instance["image_attached"];
    }

    [RelayCommand]
    private Task AddBarcodeAsync() => RunAsync(async () =>
    {
        if (!CanCreateBarcode || _variantId == 0)
            return;

        var code = NewBarcode.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException(Loc.Instance["err_fill_all"]);

        var packQty = Money.Parse(NewBarcodePackQtyText);
        if (packQty <= 0)
            throw new InvalidOperationException(Loc.Instance["barcode_invalid_pack_qty"]);

        await _barcodes.CreateAsync(new CreateBarcodeRequest(_variantId, code, packQty));
        NewBarcode = "";
        NewBarcodePackQtyText = "1";
        await LoadBarcodesAsync();
    });

    [RelayCommand]
    private Task DeleteBarcodeAsync(ProductBarcodeRow row) => RunAsync(async () =>
    {
        if (!CanDeleteBarcode)
            return;

        await _barcodes.DeleteAsync(row.Id);
        await LoadBarcodesAsync();
    });

    private async Task LoadBarcodesAsync()
    {
        Barcodes.Clear();
        foreach (var barcode in await _barcodes.GetByVariantAsync(_variantId))
            Barcodes.Add(new ProductBarcodeRow(barcode.Id, barcode.Code, barcode.PackQty));
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException(Loc.Instance["err_fill_all"]);

        var price = string.IsNullOrWhiteSpace(PriceText) ? (decimal?)null : Money.Parse(PriceText);
        var codeVal = string.IsNullOrWhiteSpace(Code) ? null : Code.Trim();

        if (_isCreate)
        {
            var units = await _units.GetAllAsync();
            var unitId = units.FirstOrDefault()?.Id ?? 0;

            var newId = await _products.CreateAsync(new CreateProductRequest(
                Name.Trim(),
                Category?.Id,
                unitId,
                0,
                _initialBarcode != null ? [new BarcodeInput(_initialBarcode)] : null,
                ImageKey: _imageKey,
                Code: codeVal,
                SellingPrice: price,
                PriceCurrency: PriceCurrency?.Code));

            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_product is null) return;

        await _products.UpdateAsync(_product.Id, new UpdateProductRequest(
            Name.Trim(),
            Category?.Id,
            _product.UnitId,
            _product.MinStock,
            _product.ProductTypeId,
            null,
            _product.Attributes,
            _imageKey,
            codeVal,
            _product.IkpuCode,
            _product.VatRate,
            price,
            PriceCurrency?.Code ?? _product.PriceCurrency,
            _product.ManufacturerId));

        WeakReferenceMessenger.Default.Send(new ProductChangedMessage(_variantId));
        Ui.Toast(Loc.Instance["saved_successfully"]);
        await Shell.Current.GoToAsync("..");
    });

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        Notice = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Ui.Toast(Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record ProductBarcodeRow(long Id, string Code, decimal PackQty)
{
    public string PackQtyText => $"× {PackQty:0.###}";
}
