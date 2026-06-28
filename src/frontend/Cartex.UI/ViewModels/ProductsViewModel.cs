using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Units;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public partial class ProductsViewModel : ViewModelBase, ILoadable
{
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IProductTypesApi _typesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    private readonly List<ProductDto> _all = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryDto? _filterCategory;
    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private CategoryDto? _editCategory;
    [ObservableProperty] private UnitDto? _editUnit;
    [ObservableProperty] private ProductTypeDto? _editProductType;
    [ObservableProperty] private decimal _editMinStock;
    [ObservableProperty] private string _editBarcodes = string.Empty;

    private long _editId;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CategoryDto> FilterCategories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];
    public ObservableCollection<ProductTypeDto> ProductTypes { get; } = [];

    public string EditTitle => IsNew ? L["add_product"] : L["edit"];
    public bool IsEmpty => Products.Count == 0;

    public ProductsViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi,
        IProductTypesApi typesApi, IToastService toast, IBusyService busy)
    {
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _unitsApi = unitsApi;
        _typesApi = typesApi;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var products = await _productsApi.GetAllAsync();
                _all.Clear();
                _all.AddRange(products);

                var categories = await _categoriesApi.GetAllAsync();
                Categories.Clear();
                FilterCategories.Clear();
                FilterCategories.Add(new CategoryDto(0, L["all"], null, null));
                foreach (var c in categories) { Categories.Add(c); FilterCategories.Add(c); }
                FilterCategory = FilterCategories[0];

                var units = await _unitsApi.GetAllAsync();
                Units.Clear();
                foreach (var u in units) Units.Add(u);

                var types = await _typesApi.GetAllAsync();
                ProductTypes.Clear();
                foreach (var t in types) ProductTypes.Add(t);

                ApplyFilter();
            }
        }
        catch
        {
            _toast.Error(L["error"]);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterCategoryChanged(CategoryDto? value) => ApplyFilter();
    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(EditTitle));

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        IEnumerable<ProductDto> source = _all;

        if (FilterCategory is { Id: > 0 })
            source = source.Where(p => p.CategoryName == FilterCategory.Name);

        if (!string.IsNullOrEmpty(query))
            source = source.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                       || p.Barcodes.Any(b => b.Contains(query, StringComparison.OrdinalIgnoreCase)));

        Products.Clear();
        foreach (var p in source)
            Products.Add(p);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = string.Empty;
        EditCategory = null;
        EditUnit = Units.FirstOrDefault();
        EditProductType = null;
        EditMinStock = 0;
        EditBarcodes = string.Empty;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(ProductDto product)
    {
        IsNew = false;
        _editId = product.Id;
        EditName = product.Name;
        EditCategory = Categories.FirstOrDefault(c => c.Name == product.CategoryName);
        EditUnit = Units.FirstOrDefault(u => u.Name == product.UnitName);
        EditProductType = ProductTypes.FirstOrDefault(t => t.Id == product.ProductTypeId);
        EditMinStock = product.MinStock;
        EditBarcodes = string.Join(", ", product.Barcodes);
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Warning(L["name"]); return; }
        if (EditUnit is null) { _toast.Warning(L["unit"]); return; }

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    var barcodes = EditBarcodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    var request = new CreateProductRequest(EditName.Trim(), EditCategory?.Id, EditUnit.Id, EditMinStock,
                        barcodes.Count > 0 ? barcodes : null, EditProductType?.Id);
                    await _productsApi.CreateAsync(request);
                }
                else
                {
                    var request = new UpdateProductRequest(EditName.Trim(), EditCategory?.Id, EditUnit.Id, EditMinStock, EditProductType?.Id);
                    await _productsApi.UpdateAsync(_editId, request);
                }
            }

            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ex is ApiException ? L["error"] : ex.Message);
        }
    }
}
