using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Catalog;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Loyalty;
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
    private readonly IManufacturersApi _manufacturersApi;
    private readonly ICatalogApi _catalogApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly ReferenceCache _cache;
    private readonly IDialogService _dialog;
    private readonly AuthService _auth;

    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private CategoryDto? _selectedCategory;
    [ObservableProperty] private UnitDto? _selectedUnit;
    [ObservableProperty] private string _barcode = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private decimal? _sellingPrice;
    [ObservableProperty] private ManufacturerDto? _selectedManufacturer;
    [ObservableProperty] private string? _unitHint;
    [ObservableProperty] private string? _categoryHint;
    [ObservableProperty] private string? _manufacturerHint;
    [ObservableProperty] private decimal _packQty = 1;
    [ObservableProperty] private bool _hasExistingBarcode;

    public long LookupWarehouseId { get; set; }

    public bool CanCreateUnit => _auth.HasPermission("units.create");
    public bool CanCreateCategory => _auth.HasPermission("categories.create");

    public event Func<long, string, Task>? Created;
    public event Func<ProductLookupDto, Task>? ExistingSelected;

    public QuickProductViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi,
        IManufacturersApi manufacturersApi, ICatalogApi catalogApi,
        IToastService toast, IBusyService busy, ReferenceCache cache, IDialogService dialog, AuthService auth)
    {
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _unitsApi = unitsApi;
        _manufacturersApi = manufacturersApi;
        _catalogApi = catalogApi;
        _toast = toast;
        _busy = busy;
        _cache = cache;
        _dialog = dialog;
        _auth = auth;
    }

    [RelayCommand]
    private async Task Open()
    {
        ResetForm();
        if (!await LoadLookupsAsync()) return;
        SelectedUnit = DefaultUnit;
        IsOpen = true;
    }

    public async Task<long> CreateFromReferenceAsync(CatalogProductDto reference, long warehouseId)
    {
        LookupWarehouseId = warehouseId;
        ResetForm();
        if (!await LoadLookupsAsync()) return 0;
        ApplyReference(reference);
        SelectedUnit ??= DefaultUnit;
        if (SelectedUnit is null) { _toast.Warning(L["unit"]); return 0; }
        return await CreateAsync();
    }

    private void ResetForm()
    {
        Name = string.Empty;
        Barcode = string.Empty;
        Code = string.Empty;
        SellingPrice = null;
        SelectedManufacturer = null;
        UnitHint = null;
        CategoryHint = null;
        ManufacturerHint = null;
        PackQty = 1;
        HasExistingBarcode = false;
        SelectedCategory = null;
        SelectedUnit = null;
    }

    private UnitDto? DefaultUnit =>
        Units.FirstOrDefault(u => u.IsDefault && u.Dimension == "Count") ?? Units.FirstOrDefault(u => u.IsDefault) ?? Units.FirstOrDefault();

    private async Task<bool> LoadLookupsAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (Units.Count == 0)
                    foreach (var u in (await _cache.GetAsync(CacheKeys.Units, () => _unitsApi.GetAllAsync())).Where(u => u.IsEnabled)) Units.Add(u);
                if (Categories.Count == 0)
                    foreach (var c in await _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync())) Categories.Add(c);
                if (Manufacturers.Count == 0)
                    foreach (var m in await _cache.GetAsync(CacheKeys.Manufacturers, () => _manufacturersApi.GetAllAsync())) Manufacturers.Add(m);
            }
            return true;
        }
        catch { _toast.Error(L["error"]); return false; }
    }

    [RelayCommand]
    private void Cancel() => IsOpen = false;

    [RelayCommand]
    private async Task LookupBarcodeAsync()
    {
        var code = Barcode.Trim();
        if (code.Length == 0) return;

        HasExistingBarcode = false;
        try
        {
            var existing = await _productsApi.GetByBarcodeAsync(code, LookupWarehouseId);
            HasExistingBarcode = true;
            var useExisting = await _dialog.ConfirmAsync(string.Format(L["barcode_already_used"], existing.ProductName), L["warning"]);
            if (useExisting && ExistingSelected is { } selected)
            {
                IsOpen = false;
                await selected(existing);
            }
            return;
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }

        try
        {
            if (await _catalogApi.GetByBarcodeAsync(code) is { } reference)
                ApplyReference(reference);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public void ApplyReference(CatalogProductDto reference)
    {
        Barcode = reference.Barcode;
        if (string.IsNullOrWhiteSpace(Name)) Name = reference.Name;
        PackQty = reference.PackQty is > 0 ? reference.PackQty.Value : 1;

        if (!string.IsNullOrWhiteSpace(reference.Unit))
        {
            SelectedUnit = Units.FirstOrDefault(unit =>
                string.Equals(unit.Name, reference.Unit, StringComparison.OrdinalIgnoreCase)
                || string.Equals(unit.ShortName, reference.Unit, StringComparison.OrdinalIgnoreCase));
            UnitHint = SelectedUnit is null ? reference.Unit : null;
        }

        var category = reference.CategoryChild ?? reference.CategoryParent;
        if (!string.IsNullOrWhiteSpace(category))
        {
            SelectedCategory = Categories.FirstOrDefault(item =>
                string.Equals(item.Name, category, StringComparison.OrdinalIgnoreCase));
            CategoryHint = SelectedCategory is null ? category : null;
        }

        if (!string.IsNullOrWhiteSpace(reference.Manufacturer))
        {
            SelectedManufacturer = Manufacturers.FirstOrDefault(manufacturer =>
                string.Equals(manufacturer.Name, reference.Manufacturer, StringComparison.OrdinalIgnoreCase));
            ManufacturerHint = SelectedManufacturer is null ? reference.Manufacturer : null;
        }
    }

    [RelayCommand]
    private async Task CreateUnit()
    {
        if (!CanCreateUnit) return;
        var name = await _dialog.PromptAsync(L["quick_create_unit"], placeholder: L["name"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var value = name.Trim();
                var id = await _unitsApi.CreateAsync(new CreateUnitRequest(value, value.Length <= 10 ? value : value[..10]));
                _cache.Invalidate(CacheKeys.Units);
                var units = (await _unitsApi.GetAllAsync()).Where(u => u.IsEnabled).ToList();
                Units.Clear();
                foreach (var unit in units) Units.Add(unit);
                SelectedUnit = Units.FirstOrDefault(u => u.Id == id);
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task CreateCategory()
    {
        if (!CanCreateCategory) return;
        var name = await _dialog.PromptAsync(L["quick_create_category"], placeholder: L["name"]);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var id = await _categoriesApi.CreateAsync(new CreateCategoryRequest(name.Trim(), null));
                _cache.Invalidate(CacheKeys.Categories);
                var categories = await _categoriesApi.GetAllAsync();
                Categories.Clear();
                foreach (var category in categories) Categories.Add(category);
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == id);
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name)) { _toast.Warning(L["name"]); return; }
        if (SelectedUnit is null) { _toast.Warning(L["unit"]); return; }

        if (!string.IsNullOrWhiteSpace(Barcode))
        {
            await LookupBarcodeAsync();
            if (HasExistingBarcode || !IsOpen) return;
        }

        await CreateAsync();
    }

    private async Task<long> CreateAsync()
    {
        if (SelectedUnit is null) return 0;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var barcodes = string.IsNullOrWhiteSpace(Barcode) ? null : new List<BarcodeInput> { new(Barcode.Trim(), PackQty > 0 ? PackQty : 1) };
                var request = new CreateProductRequest(Name.Trim(), SelectedCategory?.Id, SelectedUnit.Id, null, barcodes,
                    Code: string.IsNullOrWhiteSpace(Code) ? null : Code.Trim(),
                    SellingPrice: SellingPrice,
                    ManufacturerId: SelectedManufacturer?.Id);
                var productId = await _productsApi.CreateAsync(request);
                _cache.Invalidate(CacheKeys.ProductLookup);
                var variants = await _productsApi.GetVariantsAsync(productId);
                var variantId = variants.FirstOrDefault(v => v.IsDefault)?.Id ?? variants.FirstOrDefault()?.Id ?? 0;

                IsOpen = false;
                _toast.Success(L["success"]);
                if (variantId != 0 && Created is { } created) await created(variantId, Name.Trim());
                return variantId;
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return 0;
        }
    }
}
