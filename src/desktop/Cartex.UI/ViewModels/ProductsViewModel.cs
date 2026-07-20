using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Storage;
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
    [ObservableProperty] private ManufacturerDto? _editManufacturer;
    [ObservableProperty] private bool _isAddingManufacturer;
    [ObservableProperty] private string _newManufacturerName = string.Empty;

    [RelayCommand]
    private void ToggleAddManufacturer()
    {
        NewManufacturerName = string.Empty;
        IsAddingManufacturer = !IsAddingManufacturer;
    }

    [RelayCommand]
    private async Task AddManufacturerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewManufacturerName)) return;
        try
        {
            var api = ServiceLocator.Resolve<IManufacturersApi>();
            var id = await api.CreateAsync(new SaveManufacturerRequest(NewManufacturerName.Trim()));
            Manufacturers.Clear();
            foreach (var m in await api.GetAllAsync()) Manufacturers.Add(m);
            EditManufacturer = Manufacturers.FirstOrDefault(m => m.Id == id);
            IsAddingManufacturer = false;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
    [ObservableProperty] private string _editBarcodes = string.Empty;
    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editIkpuCode = string.Empty;
    [ObservableProperty] private decimal? _editVatRate;
    [ObservableProperty] private decimal? _editSellingPrice;
    [ObservableProperty] private string? _editImageKey;
    [ObservableProperty] private Bitmap? _editImagePreview;
    [ObservableProperty] private string? _editImageUrl;

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
    [ObservableProperty] private string? _vImageUrl;
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CategoryDto> FilterCategories { get; } = [];
    public ObservableCollection<UnitDto> Units { get; } = [];
    private readonly List<UnitDto> _allUnits = [];
    public ObservableCollection<ProductTypeDto> ProductTypes { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];

    public string EditTitle => IsNew ? L["add_product"] : L["edit"];
    public bool IsEmpty => Products.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanImport => _auth.HasPermission("products.manage");

    public ProductImportViewModel Import { get; }

    public ProductsViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi,
        IProductTypesApi typesApi, IStorageApi storageApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels,
        IPrinterService printer, IFilePickerService filePicker, IToastService toast, IBusyService busy, IExportService export, AuthService auth,
        IBusinessApi businessApi, IRatesApi ratesApi, ISettingsApi settingsApi, ReferenceCache cache, ProductImportViewModel import)
    {
        _cache = cache;
        Import = import;
        Import.Imported += () => _ = LoadAsync();
        Import.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProductImportViewModel.IsOpen)) OnPropertyChanged(nameof(IsModalOpen));
        };
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
        _settingsApi = settingsApi;
        Paging.Attach(LoadProductsAsync);
        Paging.ConfigureSort([new(L["name"], "Name"), new(L["date"], "CreatedAt")]);
        _auth.LoggedOut += ResetState;
    }

    private void ResetState()
    {
        _searchCts?.Cancel();
        _suppressReload = true;
        SearchText = string.Empty;
        FilterCategory = null;
        _suppressReload = false;
        Products.Clear();
        PriceCurrencies.Clear();
        Totals = null;
        _needsReload = false;
        IsEditOpen = false;
        IsVariantsOpen = false;
        IsVariantEditOpen = false;
        IsPrintOpen = false;
        IsAddingManufacturer = false;
        Import.IsOpen = false;
        Paging.Page = 1;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private readonly ISettingsApi _settingsApi;
    private readonly ReferenceCache _cache;
    private decimal _defaultMinStock;
    private string _baseCurrency = "UZS";
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _editPriceCurrency;
    public ObservableCollection<string> PriceCurrencies { get; } = [];

    private async Task EnsureCurrenciesAsync()
    {
        if (PriceCurrencies.Count > 0) return;
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            PriceCurrencies.Add(_baseCurrency);
            if (IsMulticurrency)
                foreach (var r in (await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync)).OrderBy(r => r.Code))
                    PriceCurrencies.Add(r.Code);
            _defaultMinStock = (await _cache.GetAsync(CacheKeys.SalesPolicy, _settingsApi.GetSalesPolicyAsync)).DefaultMinStock;
        }
        catch { }
    }

    public bool CanPrintBarcode => _auth.HasPermission("products.printBarcode");
    public bool CanToggleProduct => _auth.HasPermission("products.toggle");

    private IReadOnlyList<PageShortcut>? _pageShortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _pageShortcuts ??=
    [
        new(Key.N, KeyModifiers.Control, "shortcut_new", () => OpenCreateCommand.Execute(null), () => !Import.IsOpen, WorksInText: true),
        new(Key.F2, KeyModifiers.None, "shortcut_save", () => SaveCommand.Execute(null), () => IsEditOpen, WorksInText: true),
        new(Key.Escape, KeyModifiers.None, "shortcut_close", HandleEscape, WorksInText: true),
    ];

    private void HandleEscape()
    {
        if (Import.IsOpen) { Import.IsOpen = false; return; }
        if (IsPrintOpen) { IsPrintOpen = false; return; }
        if (IsVariantsOpen) { IsVariantsOpen = false; return; }
        if (IsEditOpen) IsEditOpen = false;
    }

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
            await _barcodesApi.GenerateAsync(_editDefaultVariantId, EditBarcodePackQty <= 0 ? 1 : EditBarcodePackQty);
            EditBarcodePackQty = 1;
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

    public ObservableCollection<ProductPackDto> EditPacks { get; } = [];
    public ObservableCollection<PackKindOption> PackKinds { get; } = [];

    [ObservableProperty] private string _packName = string.Empty;
    [ObservableProperty] private decimal _packSize = 1;
    [ObservableProperty] private PackKindOption? _selectedPackKind;
    [ObservableProperty] private bool _packIsDefault;
    private long? _editingPackId;

    private void BuildPackKinds()
    {
        PackKinds.Clear();
        PackKinds.Add(new PackKindOption("Purchase", L["pack_purchase"]));
        PackKinds.Add(new PackKindOption("Sale", L["pack_sale"]));
        PackKinds.Add(new PackKindOption("Both", L["pack_both"]));
    }

    public string PackSizeHint => EditUnit is { } unit ? $"{L["pack_size"]} ({unit.ShortName})" : L["pack_size"];

    private async Task LoadEditPacksAsync()
    {
        EditPacks.Clear();
        if (_editId == 0) return;
        try
        {
            foreach (var pack in await _productsApi.GetPacksAsync(_editId)) EditPacks.Add(pack);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SavePackAsync()
    {
        var name = PackName.Trim();
        if (_editId == 0 || name.Length == 0 || PackSize <= 0) { _toast.Warning(L["err_fill_all"]); return; }

        try
        {
            var request = new SaveProductPackRequest(name, PackSize, SelectedPackKind?.Key ?? "Purchase", PackIsDefault);
            if (_editingPackId is { } id)
                await _productsApi.UpdatePackAsync(id, request);
            else
                await _productsApi.CreatePackAsync(_editId, request);

            ResetPackForm();
            await LoadEditPacksAsync();
            _cache.Invalidate(CacheKeys.ProductLookup);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void EditPack(ProductPackDto pack)
    {
        _editingPackId = pack.Id;
        PackName = pack.Name;
        PackSize = pack.Size;
        SelectedPackKind = PackKinds.FirstOrDefault(k => k.Key == pack.Kind);
        PackIsDefault = pack.IsDefault;
    }

    [RelayCommand]
    private async Task DeletePackAsync(ProductPackDto pack)
    {
        try
        {
            await _productsApi.DeletePackAsync(pack.Id);
            if (_editingPackId == pack.Id) ResetPackForm();
            await LoadEditPacksAsync();
            _cache.Invalidate(CacheKeys.ProductLookup);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void ResetPackForm()
    {
        _editingPackId = null;
        PackName = string.Empty;
        PackSize = 1;
        SelectedPackKind = PackKinds.FirstOrDefault();
        PackIsDefault = false;
    }

    [ObservableProperty] private bool _isPrintOpen;
    [ObservableProperty] private string _printProductName = string.Empty;
    [ObservableProperty] private string _printUnitName = string.Empty;
    [ObservableProperty] private string? _printCode;
    [ObservableProperty] private int _printQuantity = 1;
    [ObservableProperty] private Bitmap? _printPreview;
    [ObservableProperty] private string? _printImageUrl;

    [ObservableProperty] private BarcodeChip? _selectedPrintBarcode;

    public ObservableCollection<BarcodeChip> PrintBarcodes { get; } = [];
    public bool HasManyBarcodes => PrintBarcodes.Count > 1;

    private long _printVariantId;

    public bool IsModalOpen => IsEditOpen || IsVariantsOpen || IsVariantEditOpen || IsPrintOpen || Import.IsOpen;
    partial void OnIsEditOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsModalOpen));
        if (!value && _needsReload)
        {
            _needsReload = false;
            _ = LoadAsync();
        }
    }
    partial void OnIsVariantsOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsVariantEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsPrintOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    [RelayCommand]
    private async Task ToggleEnabled(ProductDto product)
    {
        var enabled = product.IsEnabled;
        try
        {
            await _productsApi.SetStateAsync(product.Id, new SetProductStateRequest(!enabled));
            enabled = !enabled;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }

        var index = Products.IndexOf(product);
        if (index >= 0) Products[index] = product with { IsEnabled = enabled };
    }

    [RelayCommand]
    private async Task OpenPrintBarcode(ProductDto product)
    {
        PrintBarcodes.Clear();
        SelectedPrintBarcode = null;
        PrintPreview = null;
        PrintCode = null;
        PrintProductName = product.Name;
        PrintUnitName = product.UnitName;
        PrintImageUrl = ImageUrl.Absolute(product.ImageUrl);
        PrintQuantity = 1;
        _printVariantId = product.DefaultVariantId;
        IsPrintOpen = true;
        try
        {
            var codes = await _barcodesApi.GetByVariantAsync(_printVariantId);
            if (codes.Count == 0)
            {
                var generated = await _barcodesApi.GenerateAsync(_printVariantId);
                codes = [new BarcodeDto(0, generated, 1)];
            }

            foreach (var b in codes.OrderBy(b => b.PackQty))
                PrintBarcodes.Add(new BarcodeChip(b.Code, b.PackQty,
                    b.PackQty > 1 ? $"×{b.PackQty:0.###}" : L["unit_piece"]));

            SelectedPrintBarcode = PrintBarcodes[0];
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { OnPropertyChanged(nameof(HasManyBarcodes)); }
    }

    partial void OnSelectedPrintBarcodeChanged(BarcodeChip? value)
    {
        PrintCode = value?.Code;
        if (value is null) { PrintPreview = null; return; }
        try
        {
            using var stream = new MemoryStream(_labels.RenderPng(value.Code));
            PrintPreview = new Bitmap(stream);
        }
        catch { PrintPreview = null; }
    }

    public event Action? FocusPrintQuantityRequested;

    [RelayCommand]
    private void FocusPrintQuantity() => FocusPrintQuantityRequested?.Invoke();

    [RelayCommand]
    private void PrintQtyDec() => PrintQuantity = Math.Max(1, PrintQuantity - 1);

    [RelayCommand]
    private void PrintQtyInc() => PrintQuantity++;

    [RelayCommand]
    private void CancelPrint() => IsPrintOpen = false;

    [RelayCommand]
    private void DoPrintBarcode()
    {
        if (string.IsNullOrWhiteSpace(PrintCode) || PrintQuantity < 1) return;
        try
        {
            var name = SelectedPrintBarcode is { IsPack: true } chip ? $"{PrintProductName} {chip.Label}" : PrintProductName;
            _labels.PrintLabels(PrintCode, name, PrintQuantity, null);
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
            var url = ImageUrl.Absolute((await _storageApi.GetUrlAsync(key)).Url);
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

    [RelayCommand]
    private async Task FetchImageAsync(string target)
    {
        var isVariant = target == "variant";
        var url = (isVariant ? VImageUrl : EditImageUrl)?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var result = await _storageApi.UploadFromUrlAsync(new ImageFromUrlRequest(url));
                var preview = await LoadBitmapAsync(result.Key);

                if (isVariant) { VImageKey = result.Key; VImagePreview = preview; VImageUrl = null; }
                else { EditImageKey = result.Key; EditImagePreview = preview; EditImageUrl = null; }
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanPrintBarcode));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _suppressReload = true;

                var categoriesTask = _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync());
                var unitsTask = _cache.GetAsync(CacheKeys.Units, () => _unitsApi.GetAllAsync());
                var typesTask = _cache.GetAsync(CacheKeys.ProductTypes, () => _typesApi.GetAllAsync());
                var manufacturersTask = _cache.GetAsync(CacheKeys.Manufacturers, () => ServiceLocator.Resolve<IManufacturersApi>().GetAllAsync());
                var currenciesTask = EnsureCurrenciesAsync();

                var categories = await categoriesTask;
                Categories.Clear();
                FilterCategories.Clear();
                FilterCategories.Add(new CategoryDto(0, L["all"], null, null, null));
                foreach (var c in categories) { Categories.Add(c); FilterCategories.Add(c); }
                FilterCategory = FilterCategories[0];

                var units = await unitsTask;
                _allUnits.Clear();
                _allUnits.AddRange(units);
                Units.Clear();
                foreach (var u in units.Where(u => u.IsEnabled)) Units.Add(u);

                var types = await typesTask;
                ProductTypes.Clear();
                foreach (var t in types) ProductTypes.Add(t);

                var manufacturers = await manufacturersTask;
                Manufacturers.Clear();
                foreach (var m in manufacturers) Manufacturers.Add(m);

                await currenciesTask;

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
            var pagedTask = _productsApi.QueryAsync(QueryRequest.Create()
                .Page(Paging.Page, Paging.PageSize)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .With("categoryId", categoryId)
                .Build());
            var totalsTask = _productsApi.GetTotalsAsync(search, categoryId);
            var paged = (await pagedTask).ToPaged();
            Products.Clear();
            foreach (var p in paged.Items) Products.Add(p);
            Paging.Apply(paged.Meta);
            Totals = await totalsTask;
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
        EditUnit = Units.FirstOrDefault(u => u.IsDefault && u.Dimension == "Count") ?? Units.FirstOrDefault(u => u.IsDefault) ?? Units.FirstOrDefault();
        _pendingAttributeValues = null;
        EditProductType = null;
        EditManufacturer = null;
        EditAttributes.Clear();
        OnPropertyChanged(nameof(HasAttributes));
        EditMinStock = _defaultMinStock;
        EditBarcodes = string.Empty;
        EditCode = string.Empty;
        EditIkpuCode = string.Empty;
        EditVatRate = null;
        EditSellingPrice = null;
        EditPriceCurrency = _baseCurrency;
        EditImageKey = null;
        EditImagePreview = null;
        EditImageUrl = null;
        _editDefaultVariantId = 0;
        EditBarcodeList.Clear();
        EditBarcodeInput = string.Empty;
        EditBarcodePackQty = 1;
        EditPacks.Clear();
        BuildPackKinds();
        ResetPackForm();
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
        if (EditUnit is null && _allUnits.FirstOrDefault(u => u.Name == product.UnitName) is { } disabledUnit)
        {
            Units.Add(disabledUnit);
            EditUnit = disabledUnit;
        }
        _pendingAttributeValues = product.Attributes;
        EditProductType = null;
        EditProductType = ProductTypes.FirstOrDefault(t => t.Id == product.ProductTypeId);
        EditManufacturer = Manufacturers.FirstOrDefault(m => m.Id == product.ManufacturerId);
        EditMinStock = product.MinStock;
        EditBarcodes = string.Join(", ", product.Barcodes);
        EditCode = product.Code ?? string.Empty;
        EditIkpuCode = product.IkpuCode ?? string.Empty;
        EditVatRate = product.VatRate;
        EditSellingPrice = product.SellingPrice;
        EditPriceCurrency = product.PriceCurrency ?? _baseCurrency;
        EditImageKey = product.ImageKey;
        EditImagePreview = null;
        EditImageUrl = null;
        _ = SetPreviewAsync(product.ImageKey, b => EditImagePreview = b);
        _editDefaultVariantId = product.DefaultVariantId;
        EditBarcodeInput = string.Empty;
        EditBarcodePackQty = 1;
        BuildPackKinds();
        ResetPackForm();
        _ = LoadEditBarcodesAsync();
        _ = LoadEditPacksAsync();
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    private bool _needsReload;
    public event Action? EditNameFocusRequested;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (await SaveCoreAsync())
            IsEditOpen = false;
    }

    [RelayCommand]
    private async Task SaveAndNewAsync()
    {
        if (!await SaveCoreAsync()) return;
        ResetForNext();
        EditNameFocusRequested?.Invoke();
    }

    private void ResetForNext()
    {
        IsNew = true;
        _editId = 0;
        EditName = string.Empty;
        EditBarcodes = string.Empty;
        EditCode = string.Empty;
        EditIkpuCode = string.Empty;
        EditSellingPrice = null;
        EditImageKey = null;
        EditImagePreview = null;
        EditImageUrl = null;
        _editDefaultVariantId = 0;
        EditBarcodeList.Clear();
        EditBarcodeInput = string.Empty;
        EditBarcodePackQty = 1;
        var type = EditProductType;
        EditProductType = null;
        EditProductType = type;
    }

    private async Task<bool> SaveCoreAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Warning(L["name"]); return false; }
        if (EditUnit is null) { _toast.Warning(L["unit"]); return false; }

        var attributes = EditProductType is null ? null : AttributeSchemaCodec.SerializeValues(EditAttributes);

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    var barcodes = BarcodeSyntax.Parse(EditBarcodes);
                    var request = new CreateProductRequest(EditName.Trim(), EditCategory?.Id, EditUnit.Id, EditMinStock,
                        barcodes.Count > 0 ? barcodes : null, EditProductType?.Id,
                        Attributes: attributes,
                        Code: string.IsNullOrWhiteSpace(EditCode) ? null : EditCode.Trim(),
                        IkpuCode: string.IsNullOrWhiteSpace(EditIkpuCode) ? null : EditIkpuCode.Trim(),
                        VatRate: EditVatRate,
                        ImageKey: EditImageKey,
                        SellingPrice: EditSellingPrice,
                        PriceCurrency: IsMulticurrency ? EditPriceCurrency : null,
                        ManufacturerId: EditManufacturer?.Id);
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
                        PriceCurrency: IsMulticurrency ? EditPriceCurrency : null,
                        ManufacturerId: EditManufacturer?.Id);
                    await _productsApi.UpdateAsync(_editId, request);
                }
            }

            _needsReload = true;
            _cache.Invalidate(CacheKeys.ProductLookup);
            _toast.Success(L["success"]);
            return true;
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return false;
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
        VImageUrl = null;
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
        VBarcodes = BarcodeSyntax.Format(variant.Barcodes);
        VImageKey = variant.ImageKey;
        VImagePreview = null;
        VImageUrl = null;
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
        var barcodes = BarcodeSyntax.Parse(VBarcodes);
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

                _cache.Invalidate(CacheKeys.ProductLookup);
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
                _cache.Invalidate(CacheKeys.ProductLookup);
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

public sealed record PackKindOption(string Key, string Text);
