using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Refit;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;
using Cartex.Shared.Models.Units;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public class SupplyLine
{
    public long VariantId { get; init; }
    public string ProductName { get; init; } = "";
    public decimal Quantity { get; init; }
    public long? UnitId { get; init; }
    public string UnitName { get; init; } = "";
    public decimal PackSize { get; init; } = 1;
    public decimal PurchasePrice { get; init; }
    public decimal? SellingPrice { get; init; }
    public DateOnly? ExpiredAt { get; init; }
    public decimal LineTotal => Quantity * PurchasePrice;
    public decimal? Margin => SellingPrice is { } sp ? sp - PurchasePrice : null;
}

public partial class SuppliesViewModel : ViewModelBase, ILoadable
{
    private readonly ISuppliesApi _api;
    private readonly ISuppliersApi _suppliersApi;
    private readonly IWarehousesApi _warehousesApi;
    private readonly IProductsApi _productsApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IBarcodeLabelService _labels;
    private readonly IPrinterService _printer;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly IDialogService _dialog;

    public QuickProductViewModel QuickProduct { get; }

    public ObservableCollection<SupplyDto> Supplies { get; } = [];
    public ObservableCollection<IdOption> SupplierOptions { get; } = [];
    public ObservableCollection<IdOption> WarehouseOptions { get; } = [];
    public ObservableCollection<IdOption> ProductOptions { get; } = [];
    public ObservableCollection<UnitDto> UnitOptions { get; } = [];

    private readonly List<UnitDto> _allUnits = [];
    private readonly Dictionary<long, string> _variantDimensions = [];
    private readonly Dictionary<long, (long Id, string ShortName)> _variantStockUnits = [];
    private readonly Dictionary<long, decimal> _supplierPayables = [];
    public ObservableCollection<SupplyLine> Items { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private IdOption? _selectedSupplier;
    [ObservableProperty] private IdOption? _selectedWarehouse;
    [ObservableProperty] private DateTime _supplyDate = DateTime.Now;
    [ObservableProperty] private decimal _paidCash;
    [ObservableProperty] private decimal _paidCard;
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _supplyCurrency;

    public ObservableCollection<string> Currencies { get; } = [];
    private string _baseCurrency = "UZS";
    private bool _currenciesLoaded;
    private IBusinessApi _businessApi = null!;
    private IRatesApi _ratesApi = null!;
    private ReferenceCache _cache = null!;

    private async Task EnsureCurrenciesAsync()
    {
        if (_currenciesLoaded) return;
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            Currencies.Clear();
            Currencies.Add(_baseCurrency);
            if (IsMulticurrency)
                foreach (var r in (await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync)).OrderBy(r => r.Code))
                    Currencies.Add(r.Code);
            _currenciesLoaded = true;
        }
        catch { }
    }

    public decimal RemainsDebt => Math.Max(0, EditTotal - PaidCash - PaidCard);

    partial void OnPaidCashChanged(decimal value) => OnPropertyChanged(nameof(RemainsDebt));
    partial void OnPaidCardChanged(decimal value) => OnPropertyChanged(nameof(RemainsDebt));

    [ObservableProperty] private decimal _supplierPayable;
    [ObservableProperty] private decimal _payOldDebt;

    public bool HasSupplierDebt => SupplierPayable > 0;
    public string SupplierDebtText => string.Format(L["supplier_debt_fmt"], SupplierPayable);

    partial void OnSupplierPayableChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasSupplierDebt));
        OnPropertyChanged(nameof(SupplierDebtText));
    }

    partial void OnSelectedSupplierChanged(IdOption? value)
    {
        SupplierPayable = value?.Id is { } id && _supplierPayables.TryGetValue(id, out var p) ? p : 0;
        PayOldDebt = 0;
    }

    [ObservableProperty] private IdOption? _lineProduct;
    [ObservableProperty] private UnitDto? _lineUnit;
    [ObservableProperty] private string _lineBarcode = "";
    [ObservableProperty] private decimal _lineQuantity = 1;
    [ObservableProperty] private decimal _linePackSize = 1;
    [ObservableProperty] private decimal _linePrice;
    [ObservableProperty] private decimal _lineSellingPrice;
    [ObservableProperty] private DateTime? _lineExpiry;

    public decimal LineMargin => LineSellingPrice - LinePrice;

    partial void OnLinePriceChanged(decimal value) => OnPropertyChanged(nameof(LineMargin));
    partial void OnLineSellingPriceChanged(decimal value) => OnPropertyChanged(nameof(LineMargin));

    partial void OnLineProductChanged(IdOption? value)
    {
        RebuildUnitOptions(value);
        _ = FillPricesAsync(value);
    }

    private void RebuildUnitOptions(IdOption? product)
    {
        UnitOptions.Clear();
        if (product?.Id is not { } id) { LineUnit = null; return; }
        var dimension = _variantDimensions.TryGetValue(id, out var d) ? d : null;
        UnitDto? stocking = null;
        if (_variantStockUnits.TryGetValue(id, out var s))
        {
            stocking = _allUnits.FirstOrDefault(u => u.Id == s.Id)
                ?? new UnitDto(s.Id, s.ShortName, s.ShortName, dimension ?? "Count", 1, false);
            UnitOptions.Add(stocking);
        }
        if (dimension is not (null or "Count"))
            foreach (var u in _allUnits.Where(u => u.Dimension == dimension && u.IsEnabled && u.Id != stocking?.Id))
                UnitOptions.Add(u);
        LineUnit = stocking;
    }

    private async Task FillPricesAsync(IdOption? product)
    {
        if (product?.Id is not { } variantId || SelectedWarehouse?.Id is not { } warehouseId) return;
        if (IsMulticurrency && SupplyCurrency != _baseCurrency) return;
        var priceSnapshot = LinePrice;
        var sellingSnapshot = LineSellingPrice;
        var unitSnapshot = LineUnit;
        var packSnapshot = LinePackSize;
        try
        {
            var info = await _productsApi.GetVariantPriceInfoAsync(variantId, warehouseId);
            if (LineProduct?.Id != variantId) return;
            if (LinePrice == priceSnapshot) LinePrice = info.LastPurchasePrice ?? 0;
            if (LineSellingPrice == sellingSnapshot) LineSellingPrice = info.SellingPrice ?? 0;
            if (LineUnit == unitSnapshot && info.LastUnitId is { } lastUnit)
                LineUnit = UnitOptions.FirstOrDefault(u => u.Id == lastUnit) ?? LineUnit;
            if (LinePackSize == packSnapshot && packSnapshot == 1 && info.LastPackSize is { } lastPack && lastPack > 1)
                LinePackSize = lastPack;
        }
        catch { }
    }

    public PaginationState Paging { get; } = new();
    [ObservableProperty] private SuppliesTotalsDto? _totals;

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private IdOption? _filterSupplier;

    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private SupplyDetailDto? _detail;
    public ObservableCollection<SupplyItemDto> DetailItems { get; } = [];
    public bool CanVoid => _auth.HasPermission("supplies.manage");

    public bool IsModalOpen => IsEditOpen || IsDetailOpen || IsPrintOpen || QuickProduct.IsOpen;

    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsPrintOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsEmpty => Supplies.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public decimal EditTotal => Items.Sum(i => i.LineTotal);

    public SuppliesViewModel(ISuppliesApi api, ISuppliersApi suppliersApi, IWarehousesApi warehousesApi, IProductsApi productsApi,
        IUnitsApi unitsApi, IBarcodesApi barcodesApi, IBarcodeLabelService labels, IPrinterService printer, QuickProductViewModel quickProduct, IToastService toast, IBusyService busy,
        IExportService export, AuthService auth, IBusinessApi businessApi, IRatesApi ratesApi, ReferenceCache cache, IDialogService dialog)
    {
        _dialog = dialog;
        _cache = cache;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _api = api;
        _suppliersApi = suppliersApi;
        _warehousesApi = warehousesApi;
        _productsApi = productsApi;
        _unitsApi = unitsApi;
        _barcodesApi = barcodesApi;
        _labels = labels;
        _printer = printer;
        QuickProduct = quickProduct;
        QuickProduct.Created += OnQuickProductCreated;
        QuickProduct.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(QuickProductViewModel.IsOpen)) OnPropertyChanged(nameof(IsModalOpen)); };
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadSuppliesAsync);
    }

    private async Task LoadSuppliesAsync()
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var pagedTask = _api.QueryAsync(QueryRequest.Create()
                .Page(Paging.Page, Paging.PageSize)
                .Sort(Paging.SortBy, Paging.Descending)
                .With("fromDate", from)
                .With("toDate", to)
                .With("supplierId", FilterSupplier?.Id)
                .Build());
            var totalsTask = _api.GetTotalsAsync(null, from, to, FilterSupplier?.Id);
            var paged = (await pagedTask).ToPaged();
            Supplies.Clear();
            foreach (var x in paged.Items) Supplies.Add(x);
            Paging.Apply(paged.Meta);
            Totals = await totalsTask;
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadSuppliesAsync(); }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var all = await _api.GetAllAsync(null, from, to, FilterSupplier?.Id);
            await _export.ExportAsync(L["supplies"], all,
            [
                new(L["supply_date"], x => x.SupplyDate),
                new(L["supplier"], x => x.SupplierName),
                new(L["warehouse"], x => x.WarehouseName),
                new(L["total"], x => x.TotalAmount),
                new(L["user"], x => x.UserName),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task OpenDetail(SupplyDto supply)
    {
        if (supply is null) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                Detail = await _api.GetByIdAsync(supply.Id);
            DetailItems.Clear();
            foreach (var i in Detail.Items) DetailItems.Add(i);
            IsDetailOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseDetail() => IsDetailOpen = false;

    [RelayCommand]
    private async Task VoidSupplyAsync()
    {
        if (Detail is null) return;
        if (!await _dialog.ConfirmDangerAsync(L["supply_void_confirm"], L["supply_void"])) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.DeleteAsync(Detail.Id);
            IsDetailOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [ObservableProperty] private bool _isPrintOpen;
    [ObservableProperty] private string _printProductName = string.Empty;
    [ObservableProperty] private string? _printCode;
    [ObservableProperty] private int _printQuantity = 1;
    [ObservableProperty] private Bitmap? _printPreview;

    [RelayCommand]
    private async Task OpenPrintBarcode(SupplyLine line)
    {
        PrintProductName = line.ProductName;
        PrintQuantity = 1;
        PrintCode = null;
        PrintPreview = null;
        IsPrintOpen = true;
        try
        {
            var existing = await _barcodesApi.GetByVariantAsync(line.VariantId);
            PrintCode = existing.OrderBy(b => b.PackQty).FirstOrDefault()?.Code
                ?? await _barcodesApi.GenerateAsync(line.VariantId);
            using var stream = new MemoryStream(_labels.RenderPng(PrintCode));
            PrintPreview = new Bitmap(stream);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CancelPrint() => IsPrintOpen = false;

    [RelayCommand]
    private void DoPrintBarcode()
    {
        if (string.IsNullOrWhiteSpace(PrintCode) || PrintQuantity < 1) return;
        try
        {
            _labels.PrintLabels(PrintCode, PrintProductName, PrintQuantity, null);
            IsPrintOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async void OnQuickProductCreated(long variantId, string name)
    {
        if (QuickProduct.SelectedUnit is { } su)
        {
            _variantDimensions[variantId] = su.Dimension;
            _variantStockUnits[variantId] = (su.Id, su.ShortName);
        }
        var option = new IdOption(variantId, name);
        ProductOptions.Add(option);
        LineProduct = option;

        if (!string.IsNullOrWhiteSpace(QuickProduct.Barcode)) return;
        try
        {
            var code = await _barcodesApi.GenerateAsync(variantId);
            _toast.Success($"{L["barcode"]}: {code}");
            if (!string.IsNullOrWhiteSpace(_printer.BarcodePrinter))
                _labels.PrintLabels(code, name, 1, _printer.BarcodePrinter);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task OpenQuickCreate() => QuickProduct.OpenCommand.ExecuteAsync(null);

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var suppliersTask = _suppliersApi.GetAllAsync();
                var warehousesTask = _cache.GetAsync(CacheKeys.Warehouses, () => _warehousesApi.GetAllAsync());
                var productsTask = _cache.GetAsync(CacheKeys.ProductLookup, _productsApi.GetLookupAsync);
                var unitsTask = _cache.GetAsync(CacheKeys.Units, () => _unitsApi.GetAllAsync());

                var suppliers = await suppliersTask;
                SupplierOptions.Clear();
                _supplierPayables.Clear();
                foreach (var s in suppliers)
                {
                    SupplierOptions.Add(new IdOption(s.Id, s.Name));
                    _supplierPayables[s.Id] = s.Payable;
                }
                SupplierPayable = SelectedSupplier?.Id is { } sid && _supplierPayables.TryGetValue(sid, out var payable) ? payable : 0;

                var warehouses = await warehousesTask;
                WarehouseOptions.Clear();
                foreach (var w in warehouses) WarehouseOptions.Add(new IdOption(w.Id, w.Name));

                var products = await productsTask;
                ProductOptions.Clear();
                _variantDimensions.Clear();
                _variantStockUnits.Clear();
                foreach (var p in products)
                {
                    ProductOptions.Add(new IdOption(p.DefaultVariantId, p.Name));
                    if (p.Dimension is { } dim) _variantDimensions[p.DefaultVariantId] = dim;
                    if (p.UnitId is { } unitId) _variantStockUnits[p.DefaultVariantId] = (unitId, p.UnitShortName ?? "");
                }

                var units = await unitsTask;
                _allUnits.Clear();
                _allUnits.AddRange(units);
                RebuildUnitOptions(LineProduct);

                await LoadSuppliesAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        SelectedSupplier = SupplierOptions.FirstOrDefault();
        SelectedWarehouse = WarehouseOptions.FirstOrDefault();
        SupplyDate = DateTime.Now;
        PaidCash = 0;
        PaidCard = 0;
        PayOldDebt = 0;
        _ = EnsureCurrenciesAsync();
        SupplyCurrency = _baseCurrency;
        Items.Clear();
        ResetLine();
        OnPropertyChanged(nameof(EditTotal));
        OnPropertyChanged(nameof(RemainsDebt));
        IsEditOpen = true;
    }

    private void ResetLine()
    {
        LineProduct = ProductOptions.FirstOrDefault();
        LineUnit = null;
        LineBarcode = "";
        LineQuantity = 1;
        LinePackSize = 1;
        LinePrice = 0;
        LineSellingPrice = 0;
        LineExpiry = null;
    }

    [RelayCommand]
    private async Task ScanLineAsync()
    {
        var code = LineBarcode.Trim();
        if (string.IsNullOrEmpty(code)) return;
        if (SelectedWarehouse?.Id is null) { _toast.Warning(L["select_warehouse"]); return; }
        try
        {
            var found = await _productsApi.GetByBarcodeAsync(code, SelectedWarehouse.Id.Value);
            _variantDimensions[found.VariantId] = found.Dimension;
            var option = ProductOptions.FirstOrDefault(o => o.Id == found.VariantId) ?? new IdOption(found.VariantId, found.ProductName);
            if (!ProductOptions.Contains(option)) ProductOptions.Add(option);
            LineProduct = option;
            if (found.PackQty > 1)
            {
                LinePackSize = found.PackQty;
                _toast.Info($"{found.ProductName} ×{found.PackQty:0.###}");
            }
            LineBarcode = "";
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            LineBarcode = "";
            await QuickProduct.OpenCommand.ExecuteAsync(null);
            QuickProduct.Barcode = code;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void AddLine()
    {
        if (LineProduct?.Id is null || LineQuantity <= 0 || LinePrice < 0) { _toast.Error(L["error"]); return; }
        var packSize = LinePackSize <= 0 ? 1 : LinePackSize;
        var stockingId = _variantStockUnits.TryGetValue(LineProduct.Id.Value, out var stocking) ? stocking.Id : (long?)null;
        Items.Add(new SupplyLine
        {
            VariantId = LineProduct.Id.Value,
            ProductName = LineProduct.Name,
            Quantity = LineQuantity * packSize,
            UnitId = LineUnit?.Id is { } unitId && unitId != stockingId ? unitId : null,
            UnitName = LineUnit?.ShortName ?? "",
            PackSize = packSize,
            PurchasePrice = LinePrice,
            SellingPrice = LineSellingPrice > 0 ? LineSellingPrice : null,
            ExpiredAt = LineExpiry is { } e ? DateOnly.FromDateTime(e.Date) : null
        });
        ResetLine();
        OnPropertyChanged(nameof(EditTotal));
        OnPropertyChanged(nameof(RemainsDebt));
    }

    [RelayCommand]
    private void RemoveLine(SupplyLine line)
    {
        Items.Remove(line);
        OnPropertyChanged(nameof(EditTotal));
        OnPropertyChanged(nameof(RemainsDebt));
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSupplier?.Id is null || SelectedWarehouse?.Id is null || Items.Count == 0) { _toast.Error(L["error"]); return; }
        try
        {
            var request = new CreateSupplyRequest(
                SelectedSupplier.Id.Value,
                SelectedWarehouse.Id.Value,
                DateOnly.FromDateTime(SupplyDate.Date),
                [.. Items.Select(i => new CreateSupplyItemRequest(i.VariantId, i.Quantity, i.PurchasePrice, i.ExpiredAt, i.UnitId, i.SellingPrice, i.PackSize))],
                PaidCash,
                PaidCard,
                IsMulticurrency && SupplyCurrency != _baseCurrency ? SupplyCurrency : null);
            using (_busy.Begin(L["loading"]))
                await _api.CreateAsync(request);
            if (PayOldDebt > 0)
            {
                try { await _suppliersApi.PayDebtAsync(SelectedSupplier.Id.Value, new PaySupplierDebtRequest(PayOldDebt, false)); }
                catch { _toast.Error(L["err_debt_pay_failed"]); }
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
