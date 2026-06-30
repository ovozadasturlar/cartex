using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Units;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public partial class QuickProductViewModel : ViewModelBase
{
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

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

    public QuickProductViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi, IToastService toast, IBusyService busy)
    {
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _unitsApi = unitsApi;
        _toast = toast;
        _busy = busy;
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
                    foreach (var u in await _unitsApi.GetAllAsync()) Units.Add(u);
                    foreach (var c in await _categoriesApi.GetAllAsync()) Categories.Add(c);
                }
            }
        }
        catch { _toast.Error(L["error"]); return; }

        SelectedUnit = Units.FirstOrDefault();
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
                var barcodes = string.IsNullOrWhiteSpace(Barcode) ? null : new List<string> { Barcode.Trim() };
                var request = new CreateProductRequest(Name.Trim(), SelectedCategory?.Id, SelectedUnit.Id, 0, barcodes,
                    Code: string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                    SellingPrice: SellingPrice);
                var productId = await _productsApi.CreateAsync(request);
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
