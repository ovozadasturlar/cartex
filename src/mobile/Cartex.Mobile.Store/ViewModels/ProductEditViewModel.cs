using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProductEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly IProductsApi _products;
    private readonly ICategoriesApi _categories;
    private readonly IStorageApi _storage;
    private readonly ImageUrlBuilder _images;
    private readonly MobilePermissions _permissions;

    private long _variantId;
    private ProductDto? _product;
    private string? _imageKey;

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _priceText = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _imageLinkText = "";
    [ObservableProperty] private CategoryDto? _category;
    [ObservableProperty] private string? _previewUrl;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _notice;

    public bool CanEdit => _permissions.Has("products.edit");

    public ProductEditViewModel(
        IProductsApi products,
        ICategoriesApi categories,
        IStorageApi storage,
        ImageUrlBuilder images,
        MobilePermissions permissions)
    {
        _products = products;
        _categories = categories;
        _storage = storage;
        _images = images;
        _permissions = permissions;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id))
            _variantId = long.TryParse(Convert.ToString(id), out var parsed) ? parsed : 0;
    }

    public async Task AppearAsync()
    {
        if (!CanEdit || _product is not null) return;
        await RunAsync(async () =>
        {
            _product = (await _products.GetAllAsync(variantId: _variantId)).FirstOrDefault()
                ?? throw new InvalidOperationException(Loc.Instance["err_not_found"]);

            foreach (var c in await _categories.GetAllAsync())
                Categories.Add(c);

            Name = _product.Name;
            Code = _product.Code ?? "";
            PriceText = _product.SellingPrice?.ToString("0.##") ?? "";
            _imageKey = _product.ImageKey;
            PreviewUrl = _images.Full(_product.ImageUrl);
            Category = Categories.FirstOrDefault(c => c.Id == _product.CategoryId);
        });
    }

    [RelayCommand]
    private Task TakePhotoAsync() => CaptureAsync(() => MediaPicker.Default.CapturePhotoAsync());

    [RelayCommand]
    private Task PickPhotoAsync() => CaptureAsync(async () => (await MediaPicker.Default.PickPhotosAsync())?.FirstOrDefault());

    private Task CaptureAsync(Func<Task<FileResult?>> pick) => RunAsync(async () =>
    {
        var file = await pick();
        if (file is null) return;

        await using var stream = await file.OpenReadAsync();
        var result = await _storage.UploadAsync(new Refit.StreamPart(stream, file.FileName, file.ContentType));
        ApplyImage(result);
    });

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
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (_product is null) return;
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidOperationException(Loc.Instance["err_name_required"]);

        var price = string.IsNullOrWhiteSpace(PriceText) ? (decimal?)null : Money.Parse(PriceText);

        await _products.UpdateAsync(_product.Id, new UpdateProductRequest(
            Name.Trim(),
            Category?.Id,
            _product.UnitId,
            _product.MinStock,
            _product.ProductTypeId,
            null,
            _product.Attributes,
            _imageKey,
            string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
            _product.IkpuCode,
            _product.VatRate,
            price,
            _product.PriceCurrency,
            _product.ManufacturerId));

        Ui.Toast(Loc.Instance["saved"]);
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
        }
        finally
        {
            IsBusy = false;
        }
    }
}
