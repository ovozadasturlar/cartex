using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Units;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class QuickProductViewModel : ViewModelBase
{
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly ReferenceCache _cache;

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private UnitDto? _selectedUnit;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private decimal? _sellingPrice;

    public event Action<long, string>? Created;

    public QuickProductViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi, IToastService toast, IBusyService busy, ReferenceCache cache)
    {
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _unitsApi = unitsApi;
        _toast = toast;
        _busy = busy;
        _cache = cache;
    }

    [RelayCommand]
    private async Task Open()
    {
        Name = string.Empty;
        Barcode = string.Empty;
        Code = string.Empty;
        SellingPrice = null;
        SelectedCategory = null;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (Units.Count == 0)
                {
                    foreach (var u in (await _cache.GetAsync(CacheKeys.Units, () => _unitsApi.GetAllAsync())).Where(u => u.IsEnabled)) Units.Add(u);
                    foreach (var c in await _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync())) Categories.Add(c);
                }
            }
        }
        catch { _toast.Error(L["error"]); return; }

        SelectedUnit = Units.FirstOrDefault(u => u.IsDefault && u.Dimension == "Count") ?? Units.FirstOrDefault(u => u.IsDefault) ?? Units.FirstOrDefault();
        IsOpen = true;
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name)) { _toast.Warning(L["name"]); return; }
        if (SelectedUnit is null) { _toast.Warning(L["unit"]); return; }

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var barcodes = string.IsNullOrWhiteSpace(Barcode) ? null : new List<BarcodeInput> { new(Barcode.Trim()) };
                var request = new CreateProductRequest(Name.Trim(), SelectedCategory?.Id, SelectedUnit.Id, null, barcodes,
                    Code: string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                    SellingPrice: SellingPrice);
                var productId = await _productsApi.CreateAsync(request);
                _cache.Invalidate(CacheKeys.ProductLookup);
                var variants = await _productsApi.GetVariantsAsync(productId);
                var variantId = variants.FirstOrDefault(v => v.IsDefault)?.Id ?? variants.FirstOrDefault()?.Id ?? 0;

                IsOpen = false;
                _toast.Success(L["success"]);
                if (variantId != 0) Created?.Invoke(variantId, Name.Trim());
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }
}
