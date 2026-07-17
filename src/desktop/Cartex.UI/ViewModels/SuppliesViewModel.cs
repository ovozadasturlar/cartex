using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Refit;
using Cartex.Shared.Models.Barcodes;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;
using Cartex.Shared.Models.Units;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class SupplyLine : ObservableObject
{
    public long VariantId { get; init; }
    public string ProductName { get; init; } = "";
    public string? ImageKey { get; init; }

    public long? UnitId { get; init; }
    public long? PackId { get; init; }
    public string UnitName { get; init; } = "";
    public string StockingUnitName { get; init; } = "";
    public decimal Ratio { get; init; } = 1;
    public bool PricePerStockingUnit { get; init; }
    public DateOnly? ExpiredAt { get; set; }

    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private decimal _purchasePrice;
    [ObservableProperty] private decimal? _sellingPrice;

    public decimal StockingQuantity => Quantity * Ratio;
    public decimal PricePerStockingUnitValue =>
        PricePerStockingUnit || Ratio == 0 ? PurchasePrice : PurchasePrice / Ratio;

    public decimal LineTotal => Quantity * (PricePerStockingUnit ? PurchasePrice * Ratio : PurchasePrice);
    public decimal? Margin => SellingPrice is { } sp ? sp - PricePerStockingUnitValue : null;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(StockingQuantity));
    }

    partial void OnPurchasePriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(PricePerStockingUnitValue));
        OnPropertyChanged(nameof(Margin));
    }

    partial void OnSellingPriceChanged(decimal? value) => OnPropertyChanged(nameof(Margin));
}

public partial class SupplyPaymentLine : ObservableObject
{
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _mode = "Cash";
    [ObservableProperty] private string _currency = "";

    public string ModeText => PayMode.TextFor(Mode);

    partial void OnModeChanged(string value) => OnPropertyChanged(nameof(ModeText));
}

public sealed record SupplyEntryOption(string Display, string ShortName, long? UnitId, long? PackId, decimal Ratio)
{
    public bool IsPack => PackId is not null;
}

public sealed record BarcodeChip(string Code, decimal PackQty, string Label);

public partial class SuppliesViewModel : ViewModelBase, ILoadable
{
    private readonly ISuppliesApi _api;
    private readonly ISuppliersApi _suppliersApi;
    private readonly IWarehousesApi _warehousesApi;
    private readonly IProductsApi _productsApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IStorageApi _storageApi;
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
    public ObservableCollection<SupplyEntryOption> EntryOptions { get; } = [];

    private readonly List<UnitDto> _allUnits = [];
    private readonly Dictionary<long, string> _variantDimensions = [];
    private readonly Dictionary<long, (long Id, string ShortName)> _variantStockUnits = [];
    private readonly Dictionary<long, decimal> _supplierPayables = [];
    private readonly Dictionary<long, decimal> _lastPurchasePrices = [];
    private readonly Dictionary<long, List<ProductPackDto>> _variantPacks = [];
    private readonly Dictionary<long, string?> _variantImages = [];
    public ObservableCollection<SupplyLine> Items { get; } = [];

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private IdOption? _selectedSupplier;
    [ObservableProperty] private IdOption? _selectedWarehouse;
    [ObservableProperty] private DateTime _supplyDate = DateTime.Now;
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _supplyCurrency;

    public ObservableCollection<string> Currencies { get; } = [];
    private string _baseCurrency = "UZS";
    private IBusinessApi _businessApi = null!;
    private IRatesApi _ratesApi = null!;
    private ReferenceCache _cache = null!;

    private async Task EnsureCurrenciesAsync()
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            var selected = SupplyCurrency;
            Currencies.Clear();
            Currencies.Add(_baseCurrency);
            if (IsMulticurrency)
                foreach (var r in (await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync)).OrderBy(r => r.Code))
                    Currencies.Add(r.Code);
            SupplyCurrency = selected is not null && Currencies.Contains(selected) ? selected : _baseCurrency;
        }
        catch { }
    }

    [ObservableProperty] private bool _isPaymentOpen;
    [ObservableProperty] private decimal _paymentSupplyTotal;

    public ObservableCollection<SupplyPaymentLine> PaymentLines { get; } = [];

    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private string _paymentMode = "Cash";
    [ObservableProperty] private string? _paymentCurrency;
    [ObservableProperty] private bool _isEditingPayment;

    public ObservableCollection<PayMode> PaymentModes { get; } = [];
    [ObservableProperty] private PayMode? _selectedPaymentMode;

    partial void OnSelectedPaymentModeChanged(PayMode? value)
    {
        if (value is not null) PaymentMode = value.Key;
    }

    private void BuildPaymentModes()
    {
        PaymentModes.Clear();
        foreach (var mode in PayMode.All()) PaymentModes.Add(mode);
        SelectedPaymentMode = PaymentModes[0];
    }

    private long _paymentSupplierId;
    private long? _paymentSupplyId;
    private string? _paymentDebtCurrency;
    private SupplyPaymentLine? _editingPayment;
    private readonly Dictionary<string, decimal> _rates = [];

    private decimal ToSupplyCurrency(decimal amount, string? currency)
    {
        var supplyRate = RateOf(_paymentDebtCurrency);
        return supplyRate == 0 ? amount : amount * RateOf(currency) / supplyRate;
    }

    private decimal RateOf(string? currency) =>
        currency is null || currency == _baseCurrency ? 1 : _rates.TryGetValue(currency, out var r) && r > 0 ? r : 1;

    private decimal FromSupplyCurrency(decimal amount, string? currency)
    {
        var rate = RateOf(currency);
        return rate == 0 ? amount : amount * RateOf(_paymentDebtCurrency) / rate;
    }

    public decimal PaymentPaidTotal => PaymentLines.Sum(p => ToSupplyCurrency(p.Amount, p.Currency));
    public decimal PaymentRemains => Math.Max(0, PaymentSupplyTotal - PaymentPaidTotal);
    public bool HasPaymentLines => PaymentLines.Count > 0;

    private void RaisePaymentTotals()
    {
        OnPropertyChanged(nameof(PaymentPaidTotal));
        OnPropertyChanged(nameof(PaymentRemains));
        OnPropertyChanged(nameof(HasPaymentLines));
    }

    [RelayCommand]
    private void PayFull() => PaymentAmount = FromSupplyCurrency(PaymentRemains, PaymentCurrency);

    partial void OnPaymentCurrencyChanged(string? value)
    {
        if (IsPaymentOpen && !IsEditingPayment)
            PaymentAmount = FromSupplyCurrency(PaymentRemains, value);
    }

    [RelayCommand]
    private void AddPaymentLine()
    {
        if (PaymentAmount <= 0) { _toast.Warning(L["err_amount_positive"]); return; }

        var remains = PaymentRemains;
        if (IsEditingPayment && _editingPayment is not null)
            remains += ToSupplyCurrency(_editingPayment.Amount, _editingPayment.Currency);
        if (ToSupplyCurrency(PaymentAmount, PaymentCurrency) > remains) { _toast.Warning(L["err_overpaid"]); return; }

        if (IsEditingPayment && _editingPayment is not null)
        {
            _editingPayment.Amount = PaymentAmount;
            _editingPayment.Mode = PaymentMode;
            _editingPayment.Currency = PaymentCurrency ?? _baseCurrency;
            _editingPayment = null;
            IsEditingPayment = false;
        }
        else
            PaymentLines.Add(new SupplyPaymentLine
            {
                Amount = PaymentAmount,
                Mode = PaymentMode,
                Currency = PaymentCurrency ?? _baseCurrency
            });

        RaisePaymentTotals();
        PaymentAmount = PaymentRemains > 0 ? FromSupplyCurrency(PaymentRemains, PaymentCurrency) : 0;
    }

    [RelayCommand]
    private void EditPaymentLine(SupplyPaymentLine line)
    {
        _editingPayment = line;
        IsEditingPayment = true;
        PaymentAmount = line.Amount;
        SelectedPaymentMode = PaymentModes.FirstOrDefault(m => m.Key == line.Mode) ?? SelectedPaymentMode;
        PaymentCurrency = line.Currency;
    }

    [RelayCommand]
    private void RemovePaymentLine(SupplyPaymentLine line)
    {
        PaymentLines.Remove(line);
        if (ReferenceEquals(_editingPayment, line)) { _editingPayment = null; IsEditingPayment = false; }
        RaisePaymentTotals();
    }

    [RelayCommand]
    private void CancelPayment()
    {
        IsPaymentOpen = false;
        PaymentLines.Clear();
    }

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (PaymentLines.Count == 0) { _toast.Warning(L["err_no_payment_lines"]); return; }

        try
        {
            using (_busy.Begin(L["loading"]))
                while (PaymentLines.Count > 0)
                {
                    var line = PaymentLines[0];
                    await _suppliersApi.PayDebtAsync(_paymentSupplierId,
                        new PaySupplierDebtRequest(line.Amount, line.Mode, _paymentDebtCurrency, line.Currency, SupplyId: _paymentSupplyId));
                    PaymentLines.RemoveAt(0);
                }

            IsPaymentOpen = false;
            _toast.Success(L["success"]);
            _cache.Invalidate(CacheKeys.Suppliers);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            RaisePaymentTotals();
            _toast.Error(ApiErrors.Describe(ex));
            _cache.Invalidate(CacheKeys.Suppliers);
            await LoadAsync();
        }
    }

    private async Task EnsureRatesAsync()
    {
        try
        {
            _rates.Clear();
            foreach (var rate in await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync))
                _rates[rate.Code] = rate.Rate;
        }
        catch { }
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (Items.Count > 0 && !await _dialog.ConfirmAsync(L["clear_confirm"], L["clear"])) return;
        Items.Clear();
        ResetLine();
    }

    [ObservableProperty] private decimal _supplierPayable;

    public bool HasSupplierDebt => SupplierPayable > 0;
    public string SupplierDebtText => string.Format(L["supplier_debt_fmt"], SupplierPayable);

    partial void OnSupplierPayableChanged(decimal value)
    {
        OnPropertyChanged(nameof(HasSupplierDebt));
        OnPropertyChanged(nameof(SupplierDebtText));
    }

    partial void OnSelectedSupplierChanged(IdOption? value) =>
        SupplierPayable = value?.Id is { } id && _supplierPayables.TryGetValue(id, out var p) ? p : 0;

    [ObservableProperty] private IdOption? _lineProduct;
    [ObservableProperty] private string _lineProductText = "";
    [ObservableProperty] private SupplyEntryOption? _lineEntry;

    public event Action? FocusProductRequested;

    [RelayCommand]
    private async Task CommitProductAsync()
    {
        var name = LineProductText.Trim();
        if (name.Length == 0) return;

        if (ProductOptions.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)) is { } match)
        {
            LineProduct = match;
            return;
        }

        if (await _dialog.ConfirmAsync(string.Format(L["product_create_confirm"], name), L["add_product"]))
        {
            await QuickProduct.OpenCommand.ExecuteAsync(null);
            QuickProduct.Name = name;
            return;
        }

        FocusProductRequested?.Invoke();
    }
    [ObservableProperty] private string _lineBarcode = "";
    [ObservableProperty] private decimal _lineQuantity = 1;
    [ObservableProperty] private decimal _linePrice;
    [ObservableProperty] private decimal _lineSellingPrice;
    [ObservableProperty] private DateTime? _lineExpiry;

    [ObservableProperty] private bool _pricePerStockingUnit;

    private decimal LineRatio => LineEntry?.Ratio is { } r && r > 0 ? r : 1;

    private UnitDto? LineStockingUnit =>
        LineProduct?.Id is { } id && _variantStockUnits.TryGetValue(id, out var s)
            ? _allUnits.FirstOrDefault(u => u.Id == s.Id)
            : null;

    public string LineStockingUnitName => LineStockingUnit?.ShortName ?? LineEntry?.ShortName ?? "";
    private string LinePriceUnitName => PricePerStockingUnit ? LineStockingUnitName : LineEntry?.ShortName ?? LineStockingUnitName;
    public string LinePriceLabel => $"{L["purchase_price"]} / {LinePriceUnitName}";

    public bool CanChoosePriceBasis => LineRatio != 1;

    public decimal LinePricePerStockingUnit => PricePerStockingUnit || LineRatio == 0 ? LinePrice : LinePrice / LineRatio;

    public bool HasLinePreview => LineProduct is not null && LineRatio != 1 && LineQuantity > 0;
    public string LinePreview => HasLinePreview
        ? $"= {LineQuantity * LineRatio:0.###} {LineStockingUnitName} · {LinePricePerStockingUnit:N0} / {LineStockingUnitName}"
        : "";

    private void RaiseLinePreview()
    {
        OnPropertyChanged(nameof(LinePricePerStockingUnit));
        OnPropertyChanged(nameof(HasLinePreview));
        OnPropertyChanged(nameof(LinePreview));
        OnPropertyChanged(nameof(LinePriceLabel));
        OnPropertyChanged(nameof(LineStockingUnitName));
        OnPropertyChanged(nameof(CanChoosePriceBasis));
    }

    partial void OnLinePriceChanged(decimal value) => RaiseLinePreview();
    partial void OnLineSellingPriceChanged(decimal value) => RaiseLinePreview();
    partial void OnLineQuantityChanged(decimal value) => RaiseLinePreview();
    partial void OnPricePerStockingUnitChanged(bool value) => RaiseLinePreview();
    partial void OnLineEntryChanged(SupplyEntryOption? value) => RaiseLinePreview();

    partial void OnLineProductChanged(IdOption? value)
    {
        if (value is not null && LineProductText != value.Name) LineProductText = value.Name;
        RebuildEntryOptions(value);
        RaiseLinePreview();
        _ = FillPricesAsync(value);
    }

    private void RebuildEntryOptions(IdOption? product)
    {
        EntryOptions.Clear();
        if (product?.Id is not { } id) { LineEntry = null; return; }

        var dimension = _variantDimensions.TryGetValue(id, out var d) ? d : null;
        UnitDto? stocking = null;
        if (_variantStockUnits.TryGetValue(id, out var s))
        {
            stocking = _allUnits.FirstOrDefault(u => u.Id == s.Id)
                ?? new UnitDto(s.Id, s.ShortName, s.ShortName, dimension ?? "Count", 1, false);
            EntryOptions.Add(new SupplyEntryOption(stocking.ShortName, stocking.ShortName, stocking.Id, null, 1));
        }

        if (dimension is not (null or "Count") && stocking is { Factor: > 0 })
            foreach (var u in _allUnits.Where(u => u.Dimension == dimension && u.IsEnabled && u.Id != stocking.Id))
                EntryOptions.Add(new SupplyEntryOption(u.ShortName, u.ShortName, u.Id, null, u.Factor / stocking.Factor));

        var stockingName = stocking?.ShortName ?? "";
        if (_variantPacks.TryGetValue(id, out var packs))
            foreach (var pack in packs)
                EntryOptions.Add(new SupplyEntryOption(
                    $"{pack.Name} ({pack.Size:0.###} {stockingName})", pack.Name, null, pack.Id, pack.Size));

        LineEntry = EntryOptions.FirstOrDefault();
    }

    private async Task FillPricesAsync(IdOption? product)
    {
        if (product?.Id is not { } variantId || SelectedWarehouse?.Id is not { } warehouseId) return;
        if (IsMulticurrency && SupplyCurrency != _baseCurrency) return;
        var priceSnapshot = LinePrice;
        var sellingSnapshot = LineSellingPrice;
        var entrySnapshot = LineEntry;
        try
        {
            var info = await _productsApi.GetVariantPriceInfoAsync(variantId, warehouseId);
            if (LineProduct?.Id != variantId) return;
            if (info.LastPurchasePrice is { } lastPrice) _lastPurchasePrices[variantId] = lastPrice;

            if (LineEntry == entrySnapshot)
            {
                var restored = info.LastPackId is { } packId
                    ? EntryOptions.FirstOrDefault(o => o.PackId == packId)
                    : info.LastUnitId is { } unitId
                        ? EntryOptions.FirstOrDefault(o => o.UnitId == unitId)
                        : EntryOptions.FirstOrDefault(o => o.PackId is null && o.Ratio == 1);
                LineEntry = restored ?? LineEntry;
            }

            PricePerStockingUnit = info.LastPriceBasis == "PerStockingUnit";

            if (LinePrice == priceSnapshot)
                LinePrice = PricePerStockingUnit
                    ? info.LastPurchasePrice ?? 0
                    : (info.LastPurchasePrice ?? 0) * LineRatio;
            if (LineSellingPrice == sellingSnapshot) LineSellingPrice = info.SellingPrice ?? 0;
            RaiseLinePreview();
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

    public bool IsModalOpen => IsDetailOpen || IsPrintOpen || IsPaymentOpen || QuickProduct.IsOpen;

    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsPrintOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsPaymentOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsEmpty => Supplies.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");
    public decimal EditTotal => Items.Sum(i => i.LineTotal);
    public bool HasItems => Items.Count > 0;

    public decimal EditSellingTotal => Items.Sum(i => i.StockingQuantity * (i.SellingPrice ?? 0));
    public decimal EditProfit => Items.Where(i => i.SellingPrice is not null)
        .Sum(i => i.StockingQuantity * (i.SellingPrice!.Value - i.PricePerStockingUnitValue));

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var line in e.OldItems?.OfType<SupplyLine>() ?? []) line.PropertyChanged -= OnLineChanged;
        foreach (var line in e.NewItems?.OfType<SupplyLine>() ?? []) line.PropertyChanged += OnLineChanged;
        RaiseTotals();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SupplyLine.LineTotal) or nameof(SupplyLine.Margin)
            or nameof(SupplyLine.StockingQuantity)) RaiseTotals();
    }

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(EditTotal));
        OnPropertyChanged(nameof(EditSellingTotal));
        OnPropertyChanged(nameof(EditProfit));
        OnPropertyChanged(nameof(HasItems));
    }

    public SuppliesViewModel(ISuppliesApi api, ISuppliersApi suppliersApi, IWarehousesApi warehousesApi, IProductsApi productsApi,
        IUnitsApi unitsApi, IBarcodesApi barcodesApi, IStorageApi storageApi, IBarcodeLabelService labels, IPrinterService printer, QuickProductViewModel quickProduct, IToastService toast, IBusyService busy,
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
        _storageApi = storageApi;
        _labels = labels;
        _printer = printer;
        QuickProduct = quickProduct;
        QuickProduct.Created += OnQuickProductCreated;
        QuickProduct.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(QuickProductViewModel.IsOpen)) OnPropertyChanged(nameof(IsModalOpen)); };
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _auth.LoggedOut += ResetState;
        Items.CollectionChanged += OnItemsChanged;
        Paging.Attach(LoadSuppliesAsync);
    }

    private void ResetState()
    {
        Items.Clear();
        ResetLine();
        PaymentLines.Clear();
        IsEditOpen = false;
        IsPaymentOpen = false;
        IsDetailOpen = false;
        IsPrintOpen = false;
        SelectedSupplier = null;
        SelectedWarehouse = null;
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
    [ObservableProperty] private Bitmap? _printImage;

    [ObservableProperty] private BarcodeChip? _selectedPrintBarcode;

    public ObservableCollection<BarcodeChip> PrintBarcodes { get; } = [];
    public bool HasManyBarcodes => PrintBarcodes.Count > 1;

    private long _printVariantId;

    [RelayCommand]
    private async Task OpenPrintBarcode(SupplyLine line)
    {
        PrintBarcodes.Clear();
        SelectedPrintBarcode = null;
        PrintImage = null;
        PrintPreview = null;
        PrintCode = null;
        PrintProductName = line.ProductName;
        PrintQuantity = 1;
        _printVariantId = line.VariantId;
        IsPrintOpen = true;
        try
        {
            var codes = await _barcodesApi.GetByVariantAsync(line.VariantId);
            if (codes.Count == 0)
            {
                var generated = await _barcodesApi.GenerateAsync(line.VariantId);
                codes = [new BarcodeDto(0, generated, 1)];
            }

            foreach (var b in codes.OrderBy(b => b.PackQty))
                PrintBarcodes.Add(new BarcodeChip(b.Code, b.PackQty,
                    b.PackQty > 1 ? $"×{b.PackQty:0.###}" : L["unit_piece"]));

            SelectedPrintBarcode = PrintBarcodes[0];
            _ = SetPrintImageAsync(line.VariantId, line.ImageKey);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { OnPropertyChanged(nameof(HasManyBarcodes)); }
    }

    private async Task SetPrintImageAsync(long variantId, string? key)
    {
        var image = await LoadBitmapAsync(key);
        if (_printVariantId == variantId) PrintImage = image;
    }

    private static readonly HttpClient _imageClient = new();

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

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanVoid));
        OnPropertyChanged(nameof(CanExport));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var suppliersTask = _cache.GetAsync(CacheKeys.Suppliers, () => _suppliersApi.GetAllAsync());
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
                _variantPacks.Clear();
                _variantImages.Clear();
                foreach (var p in products)
                {
                    ProductOptions.Add(new IdOption(p.DefaultVariantId, p.Name));
                    if (p.Dimension is { } dim) _variantDimensions[p.DefaultVariantId] = dim;
                    if (p.UnitId is { } unitId) _variantStockUnits[p.DefaultVariantId] = (unitId, p.UnitShortName ?? "");
                    if (p.Packs is { Count: > 0 } packs)
                        _variantPacks[p.DefaultVariantId] = [.. packs.Where(pk => pk.Kind is "Purchase" or "Both")];
                    _variantImages[p.DefaultVariantId] = p.ImageKey;
                }

                var units = await unitsTask;
                _allUnits.Clear();
                _allUnits.AddRange(units);
                RebuildEntryOptions(LineProduct);

                await LoadSuppliesAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        if (Items.Count == 0)
        {
            SelectedSupplier ??= SupplierOptions.FirstOrDefault();
            SelectedWarehouse ??= WarehouseOptions.FirstOrDefault();
            SupplyDate = DateTime.Now;
            _ = EnsureCurrenciesAsync();
            SupplyCurrency ??= _baseCurrency;
            ResetLine();
        }

        IsEditOpen = true;
    }

    private void ResetLine()
    {
        LineProduct = null;
        LineProductText = "";
        LineEntry = null;
        PricePerStockingUnit = false;
        LineBarcode = "";
        LineQuantity = 1;
        LinePrice = 0;
        LineSellingPrice = 0;
        LineExpiry = null;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ScanLineAsync()
    {
        var code = LineBarcode.Trim();
        if (string.IsNullOrEmpty(code)) return;

        LineBarcode = "";
        if (SelectedWarehouse?.Id is not { } warehouseId) { _toast.Warning(L["select_warehouse"]); return; }

        try
        {
            var found = await _productsApi.GetByBarcodeAsync(code, warehouseId);
            _variantDimensions[found.VariantId] = found.Dimension;
            if (!ProductOptions.Any(o => o.Id == found.VariantId))
                ProductOptions.Add(new IdOption(found.VariantId, found.ProductName));

            var scanned = found.PackQty > 1 ? found.PackQty : 1;
            await AddOrMergeAsync(found.VariantId, found.ProductName, scanned, warehouseId, entry: null);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await QuickProduct.OpenCommand.ExecuteAsync(null);
            QuickProduct.Barcode = code;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task AddOrMergeAsync(long variantId, string productName, decimal quantity, long warehouseId,
        SupplyEntryOption? entry, bool pricePerStockingUnit = false,
        decimal? purchasePrice = null, decimal? sellingPrice = null, DateOnly? expiredAt = null)
    {
        var unitId = entry?.UnitId;
        var packId = entry?.PackId;

        if (Items.FirstOrDefault(i => i.VariantId == variantId && i.UnitId == unitId && i.PackId == packId
                                      && i.PricePerStockingUnit == pricePerStockingUnit
                                      && (purchasePrice is null || i.PurchasePrice == purchasePrice)
                                      && (sellingPrice is null || i.SellingPrice == sellingPrice)
                                      && (expiredAt is null || i.ExpiredAt == expiredAt)) is { } existing)
        {
            existing.Quantity += quantity;
            _toast.Info($"{productName} ×{existing.Quantity:0.###}");
            return;
        }

        var stockingUnit = _variantStockUnits.TryGetValue(variantId, out var s)
            ? _allUnits.FirstOrDefault(u => u.Id == s.Id)
            : null;

        var line = new SupplyLine
        {
            VariantId = variantId,
            ProductName = productName,
            ImageKey = _variantImages.GetValueOrDefault(variantId),
            Quantity = quantity,
            UnitId = unitId,
            PackId = packId,
            UnitName = entry?.ShortName ?? stockingUnit?.ShortName ?? "",
            StockingUnitName = stockingUnit?.ShortName ?? "",
            Ratio = entry?.Ratio is { } r && r > 0 ? r : 1,
            PricePerStockingUnit = pricePerStockingUnit,
            PurchasePrice = purchasePrice ?? 0,
            SellingPrice = sellingPrice,
            ExpiredAt = expiredAt
        };
        Items.Add(line);

        if (purchasePrice is not null) return;

        try
        {
            var info = await _productsApi.GetVariantPriceInfoAsync(variantId, warehouseId);
            if (line.PurchasePrice == 0) line.PurchasePrice = info.LastPurchasePrice ?? 0;
            if (line.SellingPrice is null or 0) line.SellingPrice = info.SellingPrice;
        }
        catch { }
    }

    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (LineProduct?.Id is not { } variantId) { _toast.Warning(L["err_select_product"]); return; }
        if (LineQuantity <= 0) { _toast.Warning(L["err_qty_positive"]); return; }
        if (LinePrice < 0) { _toast.Warning(L["err_price_negative"]); return; }
        if (SelectedWarehouse?.Id is not { } warehouseId) { _toast.Warning(L["select_warehouse"]); return; }
        if (!await ConfirmPriceAsync(variantId)) return;

        var entry = LineEntry is { Ratio: 1, PackId: null } ? null : LineEntry;

        await AddOrMergeAsync(variantId, LineProduct.Name, LineQuantity, warehouseId,
            entry, PricePerStockingUnit, LinePrice, LineSellingPrice > 0 ? LineSellingPrice : null,
            LineExpiry is { } e ? DateOnly.FromDateTime(e.Date) : null);

        ResetLine();
    }

    private async Task<bool> ConfirmPriceAsync(long variantId)
    {
        if (!_lastPurchasePrices.TryGetValue(variantId, out var last) || last <= 0) return true;
        var entered = LinePricePerStockingUnit;
        if (entered <= 0) return true;

        var ratio = entered / last;
        if (ratio < 10 && ratio > 0.1m) return true;

        var unit = LineStockingUnitName;
        var message = $"{L["price_outlier_confirm"]}\n{entered:N0} / {unit} ({L["previous"]}: {last:N0} / {unit})";
        return await _dialog.ConfirmAsync(message, L["purchase_price"]);
    }

    [RelayCommand]
    private void RemoveLine(SupplyLine line) => Items.Remove(line);

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSupplier?.Id is not { } supplierId) { _toast.Warning(L["err_select_supplier"]); return; }
        if (SelectedWarehouse?.Id is null) { _toast.Warning(L["select_warehouse"]); return; }
        if (Items.Count == 0) { _toast.Warning(L["err_no_items"]); return; }

        var supplyCurrency = IsMulticurrency && SupplyCurrency != _baseCurrency ? SupplyCurrency : null;
        var total = EditTotal;
        long supplyId;

        try
        {
            var request = new CreateSupplyRequest(
                supplierId,
                SelectedWarehouse.Id.Value,
                DateOnly.FromDateTime(SupplyDate.Date),
                [.. Items.Select(i => new CreateSupplyItemRequest(i.VariantId, i.Quantity, i.PurchasePrice, i.ExpiredAt,
                    i.UnitId, i.SellingPrice, i.PackId, i.PricePerStockingUnit ? "PerStockingUnit" : "PerEntry"))],
                0,
                0,
                supplyCurrency);
            using (_busy.Begin(L["loading"]))
                supplyId = await _api.CreateAsync(request);

            Items.Clear();
            ResetLine();
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); return; }

        if (!await _dialog.ConfirmAsync(L["supply_pay_confirm"], L["payment"])) return;

        await EnsureRatesAsync();

        _paymentSupplierId = supplierId;
        _paymentSupplyId = supplyId;
        _paymentDebtCurrency = supplyCurrency;
        _editingPayment = null;
        IsEditingPayment = false;
        PaymentLines.Clear();
        PaymentSupplyTotal = total;
        PaymentAmount = total;
        BuildPaymentModes();
        PaymentCurrency = SupplyCurrency ?? _baseCurrency;
        RaisePaymentTotals();
        IsPaymentOpen = true;
    }
}
