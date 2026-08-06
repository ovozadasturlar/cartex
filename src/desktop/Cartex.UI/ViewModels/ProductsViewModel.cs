using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Cartex.Shared.Models.Common;
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
    private readonly IFilePickerService _filePicker;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly IDialogService _dialog;

    private bool _suppressReload;

    public PaginationState Paging { get; } = new();
    [ObservableProperty] private ProductsTotalsDto _totals = new(0, 0);

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryDto? _filterCategory;
    [ObservableProperty] private decimal? _filterMinPrice;
    [ObservableProperty] private decimal? _filterMaxPrice;
    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private bool _isSaleCreate;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private CategoryDto? _editCategory;
    [ObservableProperty] private string _editDimension = "Count";
    [ObservableProperty] private UnitDto? _editUnit;
    [ObservableProperty] private bool _editAmountEntryEnabled;
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
    [ObservableProperty] private bool _isImageViewerOpen;
    [ObservableProperty] private Bitmap? _imageViewerImage;
    [ObservableProperty] private string _imageViewerTitle = string.Empty;
    private bool _imageViewerEditsCurrent;

    private long _editId;
    private string _originalDimension = "Count";

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
    public string[] DimensionOptions { get; } = ["Count", "Weight", "Volume", "Length"];
    public ObservableCollection<ProductTypeDto> ProductTypes { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];

    public string EditTitle => IsNew ? L["add_product"] : L["edit"];
    public bool CanSaveAndNew => IsNew && !IsSaleCreate;
    public bool CanConfigureAmountEntry => EditUnit?.Dimension is not null and not "Count";

    partial void OnEditDimensionChanged(string value) => ApplyUnitFilter(value, EditUnit?.Id);

    partial void OnEditUnitChanged(UnitDto? value)
    {
        OnPropertyChanged(nameof(CanConfigureAmountEntry));
        if (value?.Dimension == "Count") EditAmountEntryEnabled = false;
        else if (IsNew) EditAmountEntryEnabled = true;
    }

    private void ApplyUnitFilter(string dimension, long? preferredUnitId = null)
    {
        var preferred = _allUnits.FirstOrDefault(unit => unit.Id == preferredUnitId && unit.Dimension == dimension);
        Units.Clear();
        foreach (var unit in _allUnits.Where(unit => unit.Dimension == dimension && (unit.IsEnabled || unit.Id == preferredUnitId)))
            Units.Add(unit);
        EditUnit = preferred
            ?? Units.FirstOrDefault(unit => unit.IsDefault)
            ?? Units.FirstOrDefault();
    }
    public bool CanCreate => _auth.HasPermission("products.create");
    public bool CanEdit => _auth.HasPermission("products.edit");
    public bool CanEditCurrent => IsNew ? CanCreate : CanEdit;
    public bool CanDeleteProduct => !IsNew && _auth.HasPermission("products.delete");
    public bool IsEmpty => Products.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanImport => _auth.HasPermission("products.import");
    public bool CanCreateBarcode => _auth.HasPermission("barcodes.create");
    public bool CanDeleteBarcode => _auth.HasPermission("barcodes.delete");
    public bool CanDeleteVariant => _auth.HasPermission("products.delete");

    public ProductImportViewModel Import { get; }

    public ProductsViewModel(IProductsApi productsApi, ICategoriesApi categoriesApi, IUnitsApi unitsApi,
        IProductTypesApi typesApi, IStorageApi storageApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels,
        IFilePickerService filePicker, IPrinterService printer, IToastService toast, IBusyService busy, IExportService export, AuthService auth, IDialogService dialog,
        IBusinessApi businessApi, IRatesApi ratesApi, ISettingsApi settingsApi, ReferenceCache cache, ProductImportViewModel import, PrintDispatchService printDispatch)
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
        _filePicker = filePicker;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _dialog = dialog;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _settingsApi = settingsApi;
        BarcodePrint = new BarcodeLabelSession(barcodesApi, ratesApi, settingsApi, labels, printer, printDispatch, toast);
        BarcodePrint.PropertyChanged += OnBarcodePrintChanged;
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
        FilterMinPrice = null;
        FilterMaxPrice = null;
        _suppressReload = false;
        Products.Clear();
        PriceCurrencies.Clear();
        Totals = new ProductsTotalsDto(0, 0);
        _needsReload = false;
        IsEditOpen = false;
        IsVariantsOpen = false;
        IsVariantEditOpen = false;
        BarcodePrint.CancelCommand.Execute(null);
        IsAddingManufacturer = false;
        CloseImageViewer();
        Import.IsOpen = false;
        Paging.Page = 1;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public override void OnNavigatedFrom()
    {
        IsEditOpen = false;
        IsVariantsOpen = false;
        IsVariantEditOpen = false;
        BarcodePrint.CancelCommand.Execute(null);
        IsAddingManufacturer = false;
        CloseImageViewer();
        Import.IsOpen = false;
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
            IsMulticurrency = business.PricingMulticurrency;
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
        if (IsPrintOpen) { BarcodePrint.CancelCommand.Execute(null); return; }
        if (IsVariantsOpen) { IsVariantsOpen = false; return; }
        if (IsEditOpen) CancelEditCommand.Execute(null);
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
        if (!CanCreateBarcode) return;
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
        if (!CanDeleteBarcode) return;
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
        if (!(IsNew ? CanCreate : CanEdit)) return;
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
        if (!CanEdit) return;
        _editingPackId = pack.Id;
        PackName = pack.Name;
        PackSize = pack.Size;
        SelectedPackKind = PackKinds.FirstOrDefault(k => k.Key == pack.Kind);
        PackIsDefault = pack.IsDefault;
    }

    [RelayCommand]
    private async Task DeletePackAsync(ProductPackDto pack)
    {
        if (!CanDeleteVariant) return;
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

    public BarcodeLabelSession BarcodePrint { get; }
    public bool IsPrintOpen => BarcodePrint.IsOpen;
    public bool IsModalOpen => IsEditOpen || IsVariantsOpen || IsVariantEditOpen || IsPrintOpen || IsImageViewerOpen || Import.IsOpen;
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
    partial void OnIsImageViewerOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnImageViewerImageChanged(Bitmap? value) => OnPropertyChanged(nameof(HasImageViewerImage));
    public bool HasImageViewerImage => ImageViewerImage is not null;
    public bool CanEditViewerImage => _imageViewerEditsCurrent && CanEditCurrent;
    private void OnBarcodePrintChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BarcodeLabelSession.IsOpen)) return;
        OnPropertyChanged(nameof(IsPrintOpen));
        OnPropertyChanged(nameof(IsModalOpen));
    }

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
        await BarcodePrint.OpenAsync(new BarcodeLabelTarget(
            product.DefaultVariantId,
            product.Name,
            product.UnitName,
            ImageUrl.Absolute(product.ImageUrl),
            product.Code,
            product.SellingPrice,
            product.PriceCurrency,
            product.PriceSymbol,
            product.PriceSymbolPosition,
            product.PriceDecimalDigits));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            long? categoryId = FilterCategory is { Id: > 0 } ? FilterCategory.Id : null;
            var response = await _productsApi.QueryAsync(QueryRequest.Create()
                .Page(0, 0)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .With("categoryId", categoryId)
                .With("minPrice", FilterMinPrice)
                .With("maxPrice", FilterMaxPrice)
                .Build());
            var all = response.Content ?? [];
            await _export.ExportAsync(L["products"], all,
            [
                new(L["name"], p => p.Name, 2.0f),
                new(L["category"], p => p.CategoryName),
                new(L["unit"], p => p.UnitName, 0.6f),
                new(L["sku_code"], p => p.Code, 0.7f),
                new(L["barcode"], p => string.Join(" ", p.Barcodes), 1.4f),
                new(L["min_stock"], p => p.MinStock, 0.6f),
                new(L["ikpu_code"], p => p.IkpuCode),
                new(L["vat_rate"], p => p.VatRate, 0.5f),
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

    public async Task OpenImageViewerAsync(string? imageUrl, string title)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var bytes = await _imageClient.GetByteArrayAsync(ImageUrl.Absolute(imageUrl));
                ImageViewerImage = new Bitmap(new MemoryStream(bytes));
            }
            ImageViewerTitle = title;
            _imageViewerEditsCurrent = false;
            OnPropertyChanged(nameof(CanEditViewerImage));
            IsImageViewerOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task OpenProductImage(ProductDto product)
    {
        if (string.IsNullOrWhiteSpace(product.ImageKey)) return;
        ImageViewerImage = await LoadBitmapAsync(product.ImageKey);
        if (ImageViewerImage is null) return;
        ImageViewerTitle = product.Name;
        _imageViewerEditsCurrent = false;
        OnPropertyChanged(nameof(CanEditViewerImage));
        IsImageViewerOpen = true;
    }

    [RelayCommand]
    private void OpenEditImage()
    {
        if (EditImagePreview is null) return;
        ImageViewerImage = EditImagePreview;
        ImageViewerTitle = EditName;
        _imageViewerEditsCurrent = true;
        OnPropertyChanged(nameof(CanEditViewerImage));
        IsImageViewerOpen = true;
    }

    [RelayCommand]
    private void CloseImageViewer()
    {
        IsImageViewerOpen = false;
        ImageViewerImage = null;
        ImageViewerTitle = string.Empty;
        _imageViewerEditsCurrent = false;
        OnPropertyChanged(nameof(CanEditViewerImage));
    }

    [RelayCommand]
    private async Task DownloadImageViewerAsync()
        => await SaveImageAsync(ImageViewerImage, ImageViewerTitle);

    [RelayCommand]
    private async Task DownloadEditImageAsync()
        => await SaveImageAsync(EditImagePreview, EditName);

    private async Task SaveImageAsync(Bitmap? image, string title)
    {
        if (image is null) return;
        try
        {
            var name = string.Concat(title.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            await using var target = await _filePicker.SaveFileAsync(string.IsNullOrWhiteSpace(name) ? "product" : name, "png");
            if (target is null) return;
            image.Save(target);
            await target.FlushAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ReplaceImageViewerAsync()
    {
        if (!CanEditViewerImage) return;
        try
        {
            var uploaded = await UploadPickedImageAsync();
            if (uploaded is null) return;
            EditImageKey = uploaded.Value.Key;
            EditImagePreview = uploaded.Value.Preview;
            ImageViewerImage = uploaded.Value.Preview;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void RemoveImageViewer()
    {
        if (!CanEditViewerImage) return;
        EditImageKey = null;
        EditImagePreview = null;
        CloseImageViewer();
    }

    [RelayCommand]
    private void RemoveEditImage()
    {
        if (!CanEditCurrent) return;
        EditImageKey = null;
        EditImagePreview = null;
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
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanPrintBarcode));
        OnPropertyChanged(nameof(CanCreateBarcode));
        OnPropertyChanged(nameof(CanDeleteBarcode));
        OnPropertyChanged(nameof(CanDeleteVariant));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                if (CanCreate || CanEdit)
                {
                    _suppressReload = true;
                    await LoadEditorReferencesAsync();
                    _suppressReload = false;
                }
                else
                {
                    FilterCategories.Clear();
                    FilterCategories.Add(new CategoryDto(0, L["all"], null, null, null));
                    FilterCategory = FilterCategories[0];
                }
                await LoadProductsAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { _suppressReload = false; }
    }

    private async Task LoadEditorReferencesAsync()
    {
        var categoriesTask = _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync());
        var unitsTask = _cache.GetAsync(CacheKeys.Units, () => _unitsApi.GetAllAsync());
        var typesTask = _cache.GetAsync(CacheKeys.ProductTypes, () => _typesApi.GetAllAsync());
        var manufacturersTask = _cache.GetAsync(CacheKeys.Manufacturers, () => ServiceLocator.Resolve<IManufacturersApi>().GetAllAsync());
        var currenciesTask = EnsureCurrenciesAsync();

        var categories = await categoriesTask;
        Categories.Clear();
        FilterCategories.Clear();
        FilterCategories.Add(new CategoryDto(0, L["all"], null, null, null));
        foreach (var category in categories)
        {
            Categories.Add(category);
            FilterCategories.Add(category);
        }
        FilterCategory = FilterCategories.FirstOrDefault(category => category.Id == FilterCategory?.Id) ?? FilterCategories[0];

        var units = await unitsTask;
        _allUnits.Clear();
        _allUnits.AddRange(units);
        ApplyUnitFilter(EditDimension, EditUnit?.Id);

        var types = await typesTask;
        ProductTypes.Clear();
        foreach (var type in types) ProductTypes.Add(type);

        var manufacturers = await manufacturersTask;
        Manufacturers.Clear();
        foreach (var manufacturer in manufacturers) Manufacturers.Add(manufacturer);

        await currenciesTask;
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
                .With("minPrice", FilterMinPrice)
                .With("maxPrice", FilterMaxPrice)
                .Build());
            var totalsTask = _productsApi.GetTotalsAsync(search, categoryId, FilterMinPrice, FilterMaxPrice);
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
    partial void OnFilterMinPriceChanged(decimal? value) => ScheduleFilterReload();
    partial void OnFilterMaxPriceChanged(decimal? value) => ScheduleFilterReload();

    private void ScheduleFilterReload()
    {
        if (_suppressReload) return;
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }
    partial void OnIsNewChanged(bool value)
    {
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(CanSaveAndNew));
        OnPropertyChanged(nameof(CanDeleteProduct));
        OnPropertyChanged(nameof(CanEditCurrent));
    }

    partial void OnIsSaleCreateChanged(bool value) => OnPropertyChanged(nameof(CanSaveAndNew));

    partial void OnEditProductTypeChanged(ProductTypeDto? value)
    {
        EditAttributes.Clear();
        foreach (var f in AttributeSchemaCodec.BuildValueFields(value?.AttributeSchema, _pendingAttributeValues))
            EditAttributes.Add(f);
        _pendingAttributeValues = null;
        OnPropertyChanged(nameof(HasAttributes));
    }

    [RelayCommand]
    private async Task OpenCreateAsync() => await OpenCreateAsync(null, false);

    public async Task OpenCreateForSaleAsync(string? barcode = null) => await OpenCreateAsync(barcode, true);

    private async Task OpenCreateAsync(string? barcode, bool isSaleCreate)
    {
        if (!CanCreate) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _suppressReload = true;
                await LoadEditorReferencesAsync();
                _suppressReload = false;
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        finally { _suppressReload = false; }

        OpenCreateCore(barcode, isSaleCreate);
    }

    private void OpenCreateCore(string? barcode, bool isSaleCreate)
    {
        IsNew = true;
        IsSaleCreate = isSaleCreate;
        _editId = 0;
        EditName = string.Empty;
        EditCategory = null;
        _originalDimension = "Count";
        EditDimension = "Count";
        ApplyUnitFilter(EditDimension);
        _pendingAttributeValues = null;
        EditProductType = null;
        EditManufacturer = null;
        EditAttributes.Clear();
        OnPropertyChanged(nameof(HasAttributes));
        EditMinStock = _defaultMinStock;
        EditBarcodes = barcode ?? string.Empty;
        EditCode = string.Empty;
        EditIkpuCode = string.Empty;
        EditVatRate = null;
        EditSellingPrice = null;
        EditPriceCurrency = _baseCurrency;
        EditAmountEntryEnabled = CanConfigureAmountEntry;
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
    private async Task OpenEditAsync(ProductDto product) => await OpenEditForSaleAsync(product);

    public async Task OpenEditForSaleAsync(ProductDto product)
    {
        if (!CanEdit) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                _suppressReload = true;
                await LoadEditorReferencesAsync();
                _suppressReload = false;
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }
        finally { _suppressReload = false; }

        OpenEditCore(product);
    }

    private void OpenEditCore(ProductDto product)
    {
        IsNew = false;
        IsSaleCreate = false;
        _editId = product.Id;
        EditName = product.Name;
        EditCategory = Categories.FirstOrDefault(c => c.Name == product.CategoryName);
        var currentUnit = _allUnits.FirstOrDefault(u => u.Id == product.UnitId)
            ?? _allUnits.FirstOrDefault(u => u.Name == product.UnitName);
        _originalDimension = currentUnit?.Dimension ?? product.Dimension ?? "Count";
        EditDimension = _originalDimension;
        ApplyUnitFilter(EditDimension, currentUnit?.Id);
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
        EditAmountEntryEnabled = product.AllowsAmountEntry;
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
    private void CancelEdit()
    {
        IsSaleCreate = false;
        IsEditOpen = false;
    }

    private bool _needsReload;
    public event Action? EditNameFocusRequested;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (await SaveCoreAsync())
        {
            IsEditOpen = false;
            IsSaleCreate = false;
        }
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
        if (IsNew ? !CanCreate : !CanEdit) return false;
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Warning(L["name"]); return false; }
        if (EditUnit is null) { _toast.Warning(L["unit"]); return false; }
        if (IsNew && EditSellingPrice is null) { _toast.Warning(L["selling_price"]); return false; }

        var dimensionChanged = !IsNew && EditDimension != _originalDimension;
        if (dimensionChanged && !await _dialog.ConfirmDangerAsync(L["unit_dimension_change_confirm"], L["unit_group"]))
            return false;

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
                        ManufacturerId: EditManufacturer?.Id,
                        AmountEntryEnabled: CanConfigureAmountEntry && EditAmountEntryEnabled);
                    var productId = await _productsApi.CreateAsync(request);
                    if (IsSaleCreate)
                    {
                        var variants = await _productsApi.GetVariantsAsync(productId);
                        var variantId = variants.FirstOrDefault(variant => variant.IsDefault)?.Id ?? variants.FirstOrDefault()?.Id;
                        if (variantId is { } id && (await _productsApi.GetAllAsync(variantId: id)).FirstOrDefault() is { } product)
                            CreatedForSale?.Invoke(product);
                    }
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
                        ManufacturerId: EditManufacturer?.Id,
                        AmountEntryEnabled: CanConfigureAmountEntry && EditAmountEntryEnabled,
                        ConfirmUnitDimensionChange: dimensionChanged);
                    await _productsApi.UpdateAsync(_editId, request);
                    ProductUpdated?.Invoke(_editId);
                }
            }

            _needsReload = !IsSaleCreate;
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

    public event Action<ProductDto>? CreatedForSale;
    public event Action<long>? ProductUpdated;
    public event Action<long, IReadOnlyCollection<long>>? ProductDeleted;

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (!CanDeleteProduct || _editId == 0) return;
        if (!await _dialog.ConfirmDangerAsync(string.Format(L["delete_product_confirm"], EditName), L["delete"])) return;

        try
        {
            var productId = _editId;
            var variantIds = (await _productsApi.GetVariantsAsync(productId)).Select(v => v.Id).ToArray();
            using (_busy.Begin(L["loading"]))
                await _productsApi.DeleteAsync(productId);

            IsEditOpen = false;
            IsSaleCreate = false;
            ProductDeleted?.Invoke(productId, variantIds);
            _cache.Invalidate(CacheKeys.ProductLookup);
            _toast.Success(L["success"]);
            await LoadProductsAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
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
        if (!CanEdit) return;
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
        if (!CanEdit) return;
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
        if (!CanDeleteVariant) return;
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
