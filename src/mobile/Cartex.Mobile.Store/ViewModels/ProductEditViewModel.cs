using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Catalog;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Storage;
using Cartex.Shared.Models.Units;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProductEditViewModel : AccessAwareViewModel, IQueryAttributable
{
    private readonly IProductsApi _products;
    private readonly ICategoriesApi _categories;
    private readonly IManufacturersApi _manufacturers;
    private readonly IRatesApi _rates;
    private readonly IUnitsApi _units;
    private readonly IStorageApi _storage;
    private readonly IBarcodesApi _barcodes;
    private readonly ImageUrlBuilder _images;

    private long _variantId;
    private string? _initialBarcode;
    private CatalogProductDto? _reference;
    private decimal _initialPackQty = 1;
    private bool _isCreate;
    private ProductDto? _product;
    private string? _imageKey;
    private bool _isLoaded;

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];
    public ObservableCollection<CurrencyDto> PriceCurrencies { get; } = [];
    public ObservableCollection<ProductBarcodeRow> Barcodes { get; } = [];

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _imageLinkText = "";
    [ObservableProperty] private CategoryDto? _category;
    [ObservableProperty] private UnitDto? _unit;
    [ObservableProperty] private ManufacturerDto? _manufacturer;
    [ObservableProperty] private CurrencyDto? _priceCurrency;
    [ObservableProperty] private string? _previewUrl;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canManageBarcodes;
    [ObservableProperty] private string _newBarcode = "";
    [ObservableProperty] private string _newBarcodePackQtyText = "1";
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _notice;
    [ObservableProperty] private string? _unitHint;
    [ObservableProperty] private string? _categoryHint;
    [ObservableProperty] private string? _manufacturerHint;

    public bool CanEdit => _isCreate ? Access.CanCreateProduct : Access.CanEditProduct;
    public bool CanCreateBarcode => Access.CanCreateBarcode && !_isCreate;
    public bool CanDeleteBarcode => Access.CanDeleteBarcode && !_isCreate;
    public bool CanChoosePriceCurrency => PriceCurrencies.Count > 0;

    public ProductEditViewModel(
        IProductsApi products,
        ICategoriesApi categories,
        IManufacturersApi manufacturers,
        IRatesApi rates,
        IUnitsApi units,
        IStorageApi storage,
        IBarcodesApi barcodes,
        ImageUrlBuilder images,
        AccessState access) : base(access)
    {
        _products = products;
        _categories = categories;
        _manufacturers = manufacturers;
        _rates = rates;
        _units = units;
        _storage = storage;
        _barcodes = barcodes;
        _images = images;
        ObserveAccess(nameof(CanEdit), nameof(CanCreateBarcode), nameof(CanDeleteBarcode));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id))
            _variantId = long.TryParse(Convert.ToString(id), out var parsed) ? parsed : 0;
        if (query.TryGetValue("barcode", out var b))
            _initialBarcode = Convert.ToString(b);
        if (query.TryGetValue("reference", out var r) && r is CatalogProductDto reference)
        {
            _reference = reference;
            _initialPackQty = reference.PackQty is > 0 and { } packQty ? packQty : 1;
        }
        _isCreate = _variantId == 0;
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanCreateBarcode));
        OnPropertyChanged(nameof(CanDeleteBarcode));
        CanManageBarcodes = CanCreateBarcode || CanDeleteBarcode;
    }

    public async Task AppearAsync()
    {
        if (!CanEdit || _isLoaded) return;
        await RunAsync(async () =>
        {
            if (!_isCreate)
            {
                _product = (await _products.GetAllAsync(variantId: _variantId)).FirstOrDefault()
                    ?? throw new InvalidOperationException(Loc.Instance["product_not_found"]);
            }

            await LoadCategoriesAsync(_product?.CategoryId);
            await LoadUnitsAsync(_product?.UnitId);
            await LoadManufacturersAsync(_product?.ManufacturerId);
            await LoadPriceCurrenciesAsync();

            if (_isCreate)
            {
                Code = _initialBarcode ?? "";
                ApplyReference();
                _isLoaded = true;
                return;
            }

            var product = _product!;
            Name = product.Name;
            Code = product.Code ?? "";
            PriceText = product.SellingPrice?.ToString("0.##") ?? "";
            _imageKey = product.ImageKey;
            PreviewUrl = _images.FromKey(product.ImageKey);
            await LoadBarcodesAsync();
            _isLoaded = true;
        });
    }

    private void ApplyReference()
    {
        if (_reference is not { } reference) return;

        Name = reference.Name;

        if (!string.IsNullOrWhiteSpace(reference.Unit))
        {
            Unit = Units.FirstOrDefault(unit => Same(unit.Name, reference.Unit) || Same(unit.ShortName, reference.Unit));
            UnitHint = Unit is null ? Suggestion(reference.Unit) : null;
        }

        var categoryName = reference.CategoryChild ?? reference.CategoryParent;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            Category = Categories.FirstOrDefault(category => Same(category.Name, categoryName));
            CategoryHint = Category is null ? Suggestion(categoryName) : null;
        }

        if (!string.IsNullOrWhiteSpace(reference.Manufacturer))
        {
            Manufacturer = Manufacturers.FirstOrDefault(item => Same(item.Name, reference.Manufacturer));
            ManufacturerHint = Manufacturer is null ? Suggestion(reference.Manufacturer) : null;
        }
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string Suggestion(string? value) => $"{Loc.Instance["product_reference_suggestion"]} {value}";

    private async Task LoadCategoriesAsync(long? selectedId = null)
    {
        var categoryId = selectedId ?? Category?.Id;
        var categories = await _categories.GetAllAsync();
        Categories.Clear();
        foreach (var category in categories)
            Categories.Add(category);
        Category = categoryId is null ? null : Categories.FirstOrDefault(category => category.Id == categoryId);
    }

    private async Task LoadUnitsAsync(long? selectedId = null)
    {
        var unitId = selectedId ?? Unit?.Id;
        var units = (await _units.GetAllAsync()).Where(unit => unit.IsEnabled);
        Units.Clear();
        foreach (var unit in units)
            Units.Add(unit);
        Unit = unitId is not null
            ? Units.FirstOrDefault(unit => unit.Id == unitId)
            : Units.FirstOrDefault(unit => unit.IsDefault && string.Equals(unit.Dimension, "Count", StringComparison.Ordinal))
              ?? Units.FirstOrDefault(unit => unit.IsDefault)
              ?? Units.FirstOrDefault();
    }

    private async Task LoadManufacturersAsync(long? selectedId = null)
    {
        var manufacturerId = selectedId ?? Manufacturer?.Id;
        var manufacturers = await _manufacturers.GetAllAsync();
        Manufacturers.Clear();
        foreach (var manufacturer in manufacturers)
            Manufacturers.Add(manufacturer);
        Manufacturer = manufacturerId is null
            ? null
            : Manufacturers.FirstOrDefault(manufacturer => manufacturer.Id == manufacturerId);
    }

    private async Task LoadPriceCurrenciesAsync()
    {
        if (!Access.CanUsePricingMulticurrency)
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
    private Task AddCategoryAsync() => RunAsync(async () =>
    {
        var name = await Shell.Current.CurrentPage.DisplayPromptAsync(
            Loc.Instance["category"], Loc.Instance["name"], Loc.Instance["create"], Loc.Instance["cancel"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        var id = await _categories.CreateAsync(new CreateCategoryRequest(name.Trim(), null));
        await LoadCategoriesAsync(id);
    });

    [RelayCommand]
    private Task AddManufacturerAsync() => RunAsync(async () =>
    {
        var name = await Shell.Current.CurrentPage.DisplayPromptAsync(
            Loc.Instance["manufacturer"], Loc.Instance["name"], Loc.Instance["create"], Loc.Instance["cancel"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        var id = await _manufacturers.CreateAsync(new SaveManufacturerRequest(name.Trim()));
        await LoadManufacturersAsync(id);
    });

    [RelayCommand]
    private void ClearManufacturer() => Manufacturer = null;

    [RelayCommand]
    private Task TakePhotoAsync() => RunAsync(async () =>
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            Ui.Toast(Loc.Instance["camera_capture_unavailable"]);
            return;
        }

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
        if (string.IsNullOrWhiteSpace(Name) || Unit is null)
            throw new InvalidOperationException(Loc.Instance["err_fill_all"]);

        var price = string.IsNullOrWhiteSpace(PriceText) ? (decimal?)null : Money.Parse(PriceText);
        var codeVal = string.IsNullOrWhiteSpace(Code) ? null : Code.Trim();

        if (_isCreate)
        {
            await _products.CreateAsync(new CreateProductRequest(
                Name.Trim(),
                Category?.Id,
                Unit.Id,
                0,
                _initialBarcode != null ? [new BarcodeInput(_initialBarcode, _initialPackQty)] : null,
                ImageKey: _imageKey,
                Code: codeVal,
                SellingPrice: price,
                PriceCurrency: PriceCurrency?.Code,
                ManufacturerId: Manufacturer?.Id));

            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_product is null) return;

        await _products.UpdateAsync(_product.Id, new UpdateProductRequest(
            Name.Trim(),
            Category?.Id,
            Unit.Id,
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
            Manufacturer?.Id));

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
            Error = ex is Refit.ApiException api ? ApiErrors.Describe(api) : ex.Message;
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
