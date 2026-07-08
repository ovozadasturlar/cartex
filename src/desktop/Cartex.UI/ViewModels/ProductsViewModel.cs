using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Units;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using Refit;

namespace Cartex.UI.ViewModels;

public partial class ProductsViewModel : ViewModelBase, ILoadable
{
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IProductTypesApi _typesApi;
    private readonly IStorageApi _storageApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IBarcodeLabelService _labels;
    private readonly IPrinterService _printer;
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    private bool _suppressReload;

    public PaginationState Paging { get; } = new();
    [ObservableProperty] private ProductsTotalsDto? _totals;

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
    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editIkpuCode = string.Empty;
    [ObservableProperty] private decimal? _editVatRate;
    [ObservableProperty] private decimal? _editSellingPrice;
    [ObservableProperty] private string? _editImageKey;
    [ObservableProperty] private Bitmap? _editImagePreview;

    private long _editId;

    private string? _pendingAttributeValues;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<ProductAttributeVM> EditAttributes { get; } = [];
    public bool HasAttributes => EditAttributes.Count > 0;

    public ObservableCollection<VariantDto> Variants { get; } = [];
    public ObservableCollection<ProductAttributeVM> VAttributes { get; } = [];
    public bool VHasAttributes => VAttributes.Count > 0;
    private long _variantProductId;
    private string? _variantSchema;
    private long _variantEditId;
    [ObservableProperty] private string _variantProductName = string.Empty;
    [ObservableProperty] private bool _isVariantsOpen;
    [ObservableProperty] private bool _isVariantEditOpen;
    [ObservableProperty] private bool _isVariantNew;
    [ObservableProperty] private string _vName = string.Empty;
    [ObservableProperty] private string _vCode = string.Empty;
    [ObservableProperty] private string _vBarcodes = string.Empty;
    [ObservableProperty] private string? _vImageKey;
    [ObservableProperty] private Bitmap? _vImagePreview;
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CategoryDto> FilterCategories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];
    public ObservableCollection<ProductTypeDto> ProductTypes { get; } = [];

    public string EditTitle => IsNew ? L["add_product"] : L["edit"];
    public bool IsEmpty => Products.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    public ProductsViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi,
        IProductTypesApi typesApi, IStorageApi storageApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels,
        IPrinterService printer, IFilePickerService filePicker, IToastService toast, IBusyService busy, IExportService export, AuthService auth,
        IBusinessApi businessApi, IRatesApi ratesApi)
    {
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _unitsApi = unitsApi;
        _typesApi = typesApi;
        _storageApi = storageApi;
        _barcodesApi = barcodesApi;
        _labels = labels;
        _printer = printer;
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        Paging.Attach(LoadProductsAsync);
        Paging.ConfigureSort([new(L["name"], "Name"), new(L["date"], "CreatedAt")]);
    }

    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private string _baseCurrency = "UZS";
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _editPriceCurrency;
    public ObservableCollection<string> PriceCurrencies { get; } = [];

    private async Task EnsureCurrenciesAsync()
    {
        if (PriceCurrencies.Count > 0) return;
        try
        {
            var business = await _businessApi.GetAsync();
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            PriceCurrencies.Add(_baseCurrency);
            if (IsMulticurrency)
                foreach (var r in (await _ratesApi.GetCurrentAsync()).OrderBy(r => r.Code))
                    PriceCurrencies.Add(r.Code);
        }
        catch { }
    }

    public bool CanPrintBarcode => _auth.HasPermission("products.printBarcode");

    private long _editDefaultVariantId;
    public ObservableCollection<BarcodeDto> EditBarcodeList { get; } = [];
    [ObservableProperty] private string _editBarcodeInput = string.Empty;
    [ObservableProperty] private decimal _editBarcodePackQty = 1;

    private async Task LoadEditBarcodesAsync()
    {
        EditBarcodeList.Clear();
        if (_editDefaultVariantId == 0) return;
        var items = await _barcodesApi.GetByVariantAsync(_editDefaultVariantId);
        foreach (var b in items) EditBarcodeList.Add(b);
    }

    [RelayCommand]
    private async Task AddEditBarcode()
    {
        var code = EditBarcodeInput.Trim();
        if (code.Length == 0 || _editDefaultVariantId == 0) return;
        try
        {
            await _barcodesApi.CreateAsync(new CreateBarcodeRequest(_editDefaultVariantId, code, EditBarcodePackQty <= 0 ? 1 : EditBarcodePackQty));
            EditBarcodeInput = string.Empty;
            EditBarcodePackQty = 1;
            await LoadEditBarcodesAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task GenerateEditBarcode()
    {
        if (_editDefaultVariantId == 0) return;
        try
        {
            await _barcodesApi.GenerateAsync(_editDefaultVariantId);
            await LoadEditBarcodesAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteEditBarcode(BarcodeDto barcode)
    {
        try
        {
            await _barcodesApi.DeleteAsync(barcode.Id);
            await LoadEditBarcodesAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [ObservableProperty] private bool _isPrintOpen;
    [ObservableProperty] private string _printProductName = string.Empty;
    [ObservableProperty] private string? _printCode;
    [ObservableProperty] private int _printQuantity = 1;
    [ObservableProperty] private Bitmap? _printPreview;
    [ObservableProperty] private string? _printSelectedPrinter;
    private long _printVariantId;
    public ObservableCollection<string> PrintPrinters { get; } = [];

    [RelayCommand]
    private async Task OpenPrintBarcode(ProductDto product)
    {
        _printVariantId = product.DefaultVariantId;
        PrintProductName = product.Name;
        PrintQuantity = 1;
        PrintPrinters.Clear();
        foreach (var p in _printer.GetInstalledPrinters()) PrintPrinters.Add(p);
        PrintSelectedPrinter = _printer.BarcodePrinter ?? PrintPrinters.FirstOrDefault();
        try
        {
            PrintCode = product.Barcodes.FirstOrDefault() ?? await _barcodesApi.GenerateAsync(_printVariantId);
            UpdatePrintPreview();
            IsPrintOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void UpdatePrintPreview()
    {
        if (string.IsNullOrWhiteSpace(PrintCode)) { PrintPreview = null; return; }
        try
        {
            using var stream = new MemoryStream(_labels.RenderPng(PrintCode));
            PrintPreview = new Bitmap(stream);
        }
        catch { PrintPreview = null; }
    }

    [RelayCommand]
    private void CancelPrint() => IsPrintOpen = false;

    [RelayCommand]
    private void DoPrintBarcode()
    {
        if (string.IsNullOrWhiteSpace(PrintCode) || PrintQuantity < 1) return;
        try
        {
            _labels.PrintLabels(PrintCode, PrintProductName, PrintQuantity, PrintSelectedPrinter);
            IsPrintOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            long? categoryId = FilterCategory is { Id: > 0 } ? FilterCategory.Id : null;
            var all = await _productsApi.GetAllAsync(categoryId, search);
            await _export.ExportAsync(L["products"], all,
            [
                new(L["name"], p => p.Name),
                new(L["category"], p => p.CategoryName),
                new(L["unit"], p => p.UnitName),
                new(L["sku_code"], p => p.Code),
                new(L["barcode"], p => string.Join(" ", p.Barcodes)),
                new(L["min_stock"], p => p.MinStock),
                new(L["ikpu_code"], p => p.IkpuCode),
                new(L["vat_rate"], p => p.VatRate),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static readonly HttpClient _imageClient = new();

    private async Task SetPreviewAsync(string? key, Action<Bitmap?> set) => set(await LoadBitmapAsync(key));

    private async Task<Bitmap?> LoadBitmapAsync(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        try
        {
            var url = (await _storageApi.GetUrlAsync(key)).Url;
            var bytes = await _imageClient.GetByteArrayAsync(url);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch { return null; }
    }

    private async Task<(string Key, Bitmap? Preview)?> UploadPickedImageAsync()
    {
        var picked = await _filePicker.PickImageAsync();
        if (picked is null) return null;
        using (_busy.Begin(L["loading"]))
        await using (picked.Content)
        {
            var result = await _storageApi.UploadAsync(new StreamPart(picked.Content, picked.FileName, picked.ContentType));
            return (result.Key, await LoadBitmapAsync(result.Key));
        }
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        try
        {
            var uploaded = await UploadPickedImageAsync();
            if (uploaded is null) return;
            EditImageKey = uploaded.Value.Key;
            EditImagePreview = uploaded.Value.Preview;
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _suppressReload = true;

                var categories = await _categoriesApi.GetAllAsync();
                Categories.Clear();
                FilterCategories.Clear();
                FilterCategories.Add(new CategoryDto(0, L["all"], null, null, null));
                foreach (var c in categories) { Categories.Add(c); FilterCategories.Add(c); }
                FilterCategory = FilterCategories[0];

                var units = await _unitsApi.GetAllAsync();
                Units.Clear();
                foreach (var u in units) Units.Add(u);

                var types = await _typesApi.GetAllAsync();
                ProductTypes.Clear();
                foreach (var t in types) ProductTypes.Add(t);

                await EnsureCurrenciesAsync();

                _suppressReload = false;
                await LoadProductsAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            long? categoryId = FilterCategory is { Id: > 0 } ? FilterCategory.Id : null;
            var result = await _productsApi.GetPagedAsync(Paging.Page, Paging.PageSize, Paging.SortBy, Paging.Descending, search, categoryId);
            var paged = result.ToPaged();
            Products.Clear();
            foreach (var p in paged.Items) Products.Add(p);
            Paging.Apply(paged.Meta);
            Totals = await _productsApi.GetTotalsAsync(search, categoryId);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        if (_suppressReload) return;
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadProductsAsync();
    }

    partial void OnFilterCategoryChanged(CategoryDto? value) { if (_suppressReload) return; Paging.Page = 1; _ = LoadProductsAsync(); }
    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(EditTitle));

    partial void OnEditProductTypeChanged(ProductTypeDto? value)
    {
        EditAttributes.Clear();
        foreach (var f in AttributeSchemaCodec.BuildValueFields(value?.AttributeSchema, _pendingAttributeValues))
            EditAttributes.Add(f);
        _pendingAttributeValues = null;
        OnPropertyChanged(nameof(HasAttributes));
    }

    [RelayCommand]
    private void OpenCreate()
    {
        IsNew = true;
        _editId = 0;
        EditName = string.Empty;
        EditCategory = null;
        EditUnit = Units.FirstOrDefault();
        _pendingAttributeValues = null;
        EditProductType = null;
        EditAttributes.Clear();
        OnPropertyChanged(nameof(HasAttributes));
        EditMinStock = 0;
        EditBarcodes = string.Empty;
        EditCode = string.Empty;
        EditIkpuCode = string.Empty;
        EditVatRate = null;
        EditSellingPrice = null;
        EditPriceCurrency = _baseCurrency;
        EditImageKey = null;
        EditImagePreview = null;
        _editDefaultVariantId = 0;
        EditBarcodeList.Clear();
        EditBarcodeInput = string.Empty;
        EditBarcodePackQty = 1;
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
        _pendingAttributeValues = product.Attributes;
        EditProductType = null;
        EditProductType = ProductTypes.FirstOrDefault(t => t.Id == product.ProductTypeId);
        EditMinStock = product.MinStock;
        EditBarcodes = string.Join(", ", product.Barcodes);
        EditCode = product.Code ?? string.Empty;
        EditIkpuCode = product.IkpuCode ?? string.Empty;
        EditVatRate = product.VatRate;
        EditSellingPrice = product.SellingPrice;
        EditPriceCurrency = product.PriceCurrency ?? _baseCurrency;
        EditImageKey = product.ImageKey;
        EditImagePreview = null;
        _ = SetPreviewAsync(product.ImageKey, b => EditImagePreview = b);
        _editDefaultVariantId = product.DefaultVariantId;
        EditBarcodeInput = string.Empty;
        EditBarcodePackQty = 1;
        _ = LoadEditBarcodesAsync();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Warning(L["name"]); return; }
        if (EditUnit is null) { _toast.Warning(L["unit"]); return; }

        var attributes = EditProductType is null ? null : AttributeSchemaCodec.SerializeValues(EditAttributes);

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    var barcodes = EditBarcodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    var request = new CreateProductRequest(EditName.Trim(), EditCategory?.Id, EditUnit.Id, EditMinStock,
                        barcodes.Count > 0 ? barcodes : null, EditProductType?.Id,
                        Attributes: attributes,
                        Code: string.IsNullOrWhiteSpace(EditCode) ? null : EditCode.Trim(),
                        IkpuCode: string.IsNullOrWhiteSpace(EditIkpuCode) ? null : EditIkpuCode.Trim(),
                        VatRate: EditVatRate,
                        ImageKey: EditImageKey,
                        SellingPrice: EditSellingPrice,
                        PriceCurrency: IsMulticurrency ? EditPriceCurrency : null);
                    await _productsApi.CreateAsync(request);
                }
                else
                {
                    var request = new UpdateProductRequest(EditName.Trim(), EditCategory?.Id, EditUnit.Id, EditMinStock, EditProductType?.Id,
                        Attributes: attributes,
                        Code: string.IsNullOrWhiteSpace(EditCode) ? null : EditCode.Trim(),
                        IkpuCode: string.IsNullOrWhiteSpace(EditIkpuCode) ? null : EditIkpuCode.Trim(),
                        VatRate: EditVatRate,
                        ImageKey: EditImageKey,
                        SellingPrice: EditSellingPrice,
                        PriceCurrency: IsMulticurrency ? EditPriceCurrency : null);
                    await _productsApi.UpdateAsync(_editId, request);
                }
            }

            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    private void BuildVariantAttributes(string? valuesJson)
    {
        VAttributes.Clear();
        foreach (var f in AttributeSchemaCodec.BuildValueFields(_variantSchema, valuesJson)) VAttributes.Add(f);
        OnPropertyChanged(nameof(VHasAttributes));
    }

    private async Task ReloadVariantsAsync()
    {
        var items = await _productsApi.GetVariantsAsync(_variantProductId);
        Variants.Clear();
        foreach (var v in items) Variants.Add(v);
    }

    [RelayCommand]
    private async Task OpenVariants(ProductDto product)
    {
        _variantProductId = product.Id;
        VariantProductName = product.Name;
        _variantSchema = ProductTypes.FirstOrDefault(t => t.Id == product.ProductTypeId)?.AttributeSchema;
        try
        {
            using (_busy.Begin(L["loading"]))
                await ReloadVariantsAsync();
            IsVariantsOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseVariants() => IsVariantsOpen = false;

    [RelayCommand]
    private void OpenVariantCreate()
    {
        IsVariantNew = true;
        _variantEditId = 0;
        VName = string.Empty;
        VCode = string.Empty;
        VBarcodes = string.Empty;
        VImageKey = null;
        VImagePreview = null;
        BuildVariantAttributes(null);
        IsVariantEditOpen = true;
    }

    [RelayCommand]
    private void OpenVariantEdit(VariantDto variant)
    {
        IsVariantNew = false;
        _variantEditId = variant.Id;
        VName = variant.Name ?? string.Empty;
        VCode = variant.Code ?? string.Empty;
        VBarcodes = string.Join(", ", variant.Barcodes);
        VImageKey = variant.ImageKey;
        VImagePreview = null;
        _ = SetPreviewAsync(variant.ImageKey, b => VImagePreview = b);
        BuildVariantAttributes(variant.Attributes);
        IsVariantEditOpen = true;
    }

    [RelayCommand]
    private void CancelVariantEdit() => IsVariantEditOpen = false;

    [RelayCommand]
    private async Task PickVariantImage()
    {
        try
        {
            var uploaded = await UploadPickedImageAsync();
            if (uploaded is null) return;
            VImageKey = uploaded.Value.Key;
            VImagePreview = uploaded.Value.Preview;
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task SaveVariant()
    {
        var attributes = AttributeSchemaCodec.SerializeValues(VAttributes);
        var barcodes = VBarcodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var name = string.IsNullOrWhiteSpace(VName) ? null : VName.Trim();
        var code = string.IsNullOrWhiteSpace(VCode) ? null : VCode.Trim();

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsVariantNew)
                    await _productsApi.CreateVariantAsync(_variantProductId,
                        new CreateVariantRequest(name, code, attributes, VImageKey, barcodes.Count > 0 ? barcodes : null));
                else
                    await _productsApi.UpdateVariantAsync(_variantEditId,
                        new UpdateVariantRequest(name, code, attributes, VImageKey, barcodes.Count > 0 ? barcodes : null));

                await ReloadVariantsAsync();
            }
            IsVariantEditOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task DeleteVariant(VariantDto variant)
    {
        if (variant.IsDefault) { _toast.Warning(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _productsApi.DeleteVariantAsync(variant.Id);
                await ReloadVariantsAsync();
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }
}
