using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Prepacks;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Rates;
using Cartex.Shared.Models.Supplies;
using Cartex.Shared.Models.Shifts;
using Avalonia.Input;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public record HeldSale(string Label, List<CartItem> Items, decimal PaidCash, decimal PaidCard, decimal PaidBonus, CustomerDto? Customer, DateTime HeldAt);

public record QueueRow(Cartex.Shared.Models.Ordering.CartListDto Cart)
{
    public string Title => Cart.CreatedByName ?? "";
    public string Subtitle => $"{Cart.CreatedAt:HH:mm} · {Cart.ItemCount} · {Cart.EstimatedTotal:N0}";
    public string? Note => Cart.Note;
    public bool HasNote => !string.IsNullOrWhiteSpace(Cart.Note);
    public string? CustomerName => Cart.CustomerName;
    public bool HasCustomer => !string.IsNullOrWhiteSpace(Cart.CustomerName);
}

public record PayMethodOption(string Key, string Label);

public partial class PaymentRow : ObservableObject
{
    private readonly Action _changed;
    private readonly Func<string?, decimal> _rateOf;
    private readonly string _baseCurrency;
    private PayMethodOption _method;
    private string _currency;

    [ObservableProperty] private decimal _amount;

    public ObservableCollection<string> Currencies { get; }
    public ObservableCollection<PayMethodOption> Methods { get; }

    public PaymentRow(string currency, PayMethodOption method, ObservableCollection<string> currencies,
        ObservableCollection<PayMethodOption> methods, Action changed, Func<string?, decimal> rateOf, string baseCurrency)
    {
        _currency = currency;
        _method = method;
        Currencies = currencies;
        Methods = methods;
        _changed = changed;
        _rateOf = rateOf;
        _baseCurrency = baseCurrency;
    }

    public PayMethodOption Method
    {
        get => _method;
        set { if (value is not null && SetProperty(ref _method, value)) _changed(); }
    }

    public string Currency
    {
        get => _currency;
        set
        {
            if (value is null || !SetProperty(ref _currency, value)) return;
            OnPropertyChanged(nameof(AmountBase));
            OnPropertyChanged(nameof(ShowBase));
            _changed();
        }
    }

    public void RefreshCurrency() => OnPropertyChanged(nameof(Currency));

    public decimal AmountBase => Math.Round(Amount * _rateOf(Currency), 2);
    public bool ShowBase => Currency != _baseCurrency;

    partial void OnAmountChanged(decimal value) { OnPropertyChanged(nameof(AmountBase)); _changed(); }
}

public partial class QuickRateItem(CurrencyDto currency, bool isStale) : ObservableObject
{
    public string Code { get; } = currency.Code;
    public string Name { get; } = currency.Name;
    public decimal? CurrentRate { get; } = currency.Rate;
    public DateTime? RateAt { get; } = currency.RateAt;
    public bool IsStale { get; } = isStale;
    [ObservableProperty] private decimal _newRate = currency.Rate ?? 0;
}

public partial class CartItem : ObservableObject
{
    public long VariantId { get; init; }
    public long? PrepackId { get; init; }
    public bool IsPrepack => PrepackId is not null;
    public string ProductName { get; init; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity = 1;

    [ObservableProperty] private decimal? _available;

    partial void OnAvailableChanged(decimal? value) => OnPropertyChanged(nameof(IsOverStock));

    public decimal? PriceOverride => UnitPrice != OriginalPrice ? UnitPrice : null;
    public decimal LineTotal => UnitPrice * Quantity;
    public bool IsOverStock => Available is { } a && Quantity > a;

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(IsOverStock));
    }

    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public partial class CategoryChip(long? id, string name) : ObservableObject
{
    public long? Id { get; } = id;
    public string Name { get; } = name;
    [ObservableProperty] private bool _isSelected;
}

public partial class SalesViewModel : ViewModelBase, ILoadable
{
    private readonly ISalesApi _salesApi;
    private readonly IStocksApi _stocksApi;
    private readonly ISuppliersApi _suppliersApi;
    private readonly ISuppliesApi _suppliesApi;
    private readonly ICustomersApi _customersApi;
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IReceiptApi _receiptApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IShiftsApi _shiftsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IHeldSaleStore _heldStore;
    private readonly IPrinterService _printer;
    private readonly IScannedCodeParser _scannedCodeParser;
    private readonly AuthService _auth;

    private readonly List<CategoryDto> _allCategories = [];
    private long? _selectedCategoryId;

    private readonly IPrepacksApi _prepacksApi;
    private readonly IFeaturesApi _featuresApi;

    public BranchContextService Branch { get; }
    public QuickProductViewModel QuickProduct { get; }
    public PrepackViewModel Prepack { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _customerSearch = string.Empty;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private decimal _paidCash;
    [ObservableProperty] private decimal _paidCard;
    [ObservableProperty] private decimal _paidBonus;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private decimal _discountPercent;
    private bool _syncingDiscount;
    private bool _discountByPercent;
    [ObservableProperty] private bool _isCustomerPanelOpen;
    [ObservableProperty] private bool _isHeldPanelOpen;
    [ObservableProperty] private bool _isQueuePanelOpen;
    [ObservableProperty] private int _queueCount;
    [ObservableProperty] private bool _canSeeQueue;
    [ObservableProperty] private bool _isReceiptOpen;
    [ObservableProperty] private bool _posListMode = SettingsService.Instance.PosListMode;
    [ObservableProperty] private ReceiptDto? _currentReceipt;
    [ObservableProperty] private bool _isProductDetailOpen;
    [ObservableProperty] private StockOnHandDto? _detailProduct;
    [ObservableProperty] private string _detailBarcodes = string.Empty;
    [ObservableProperty] private bool _isReceiveOpen;
    [ObservableProperty] private decimal _receiveQuantity;
    [ObservableProperty] private decimal _receivePurchasePrice;
    [ObservableProperty] private decimal _receiveSellingPrice;
    [ObservableProperty] private DateTime? _receiveExpiry;
    [ObservableProperty] private string? _receiveCurrency;
    [ObservableProperty] private IdOption? _receiveSupplier;
    [ObservableProperty] private bool _hasNoSuppliers;
    [ObservableProperty] private bool _canReceiveStock;
    public ObservableCollection<IdOption> SupplierOptions { get; } = [];
    private long? _lastSupplierId;
    private decimal _lastPurchasePrice;
    private decimal _detailRequestedQty;

    public bool IsModalOpen => IsCustomerPanelOpen || IsProductDetailOpen || IsReceiptOpen || IsQuickRatesOpen || IsQueuePanelOpen || IsHeldPanelOpen || QuickProduct.IsOpen || Prepack.IsOpen;
    partial void OnIsCustomerPanelOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsProductDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsReceiptOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsQueuePanelOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsHeldPanelOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    partial void OnPosListModeChanged(bool value) => SettingsService.Instance.PosListMode = value;

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<StockOnHandDto> Products { get; } = [];
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    public ObservableCollection<HeldSale> HeldSales { get; } = [];
    public ObservableCollection<QueueRow> QueueCarts { get; } = [];
    public ObservableCollection<CategoryChip> NavCategories { get; } = [];
    public ObservableCollection<CategoryChip> SubCategories { get; } = [];
    [ObservableProperty] private bool _hasSubCategories;

    private const int PosPageSize = 120;
    private int _productsPage = 1;
    [ObservableProperty] private int _productsTotal;
    public bool HasMoreProducts => Products.Count < ProductsTotal;

    public Avalonia.Controls.GridLength CartWidth
    {
        get => new(SettingsService.Instance.PosCartWidth);
        set
        {
            if (value.IsAbsolute && value.Value >= 320)
                SettingsService.Instance.PosCartWidth = value.Value;
            OnPropertyChanged();
        }
    }

    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private readonly IOrderingApi _orderingApi;
    private readonly PosHandoffService _handoff;
    private readonly QueueHubService _queueHub;
    private string? _activeCartCode;
    private readonly Dictionary<string, decimal> _rates = [];
    private string _baseCurrency = "UZS";
    private string _defaultCurrency = "UZS";
    private List<CurrencyDto> _foreignCurrencies = [];

    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private bool _hasStaleRate;
    [ObservableProperty] private bool _isQuickRatesOpen;
    private string? _selectedDebtCurrency;
    [ObservableProperty] private DateTimeOffset? _debtDueDate;
    [ObservableProperty] private bool _dueDateMissing;

    partial void OnDebtDueDateChanged(DateTimeOffset? value) => DueDateMissing = false;

    public string? SelectedDebtCurrency
    {
        get => _selectedDebtCurrency;
        set { if (value is not null && SetProperty(ref _selectedDebtCurrency, value)) OnPropertyChanged(nameof(DebtDisplay)); }
    }

    public ObservableCollection<QuickRateItem> QuickRates { get; } = [];
    public bool CanManageRates => _auth.HasPermission("rates.manage");
    public bool ShowStaleFix => HasStaleRate && CanManageRates;
    public bool ShowStaleHint => HasStaleRate && !CanManageRates;

    partial void OnHasStaleRateChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStaleFix));
        OnPropertyChanged(nameof(ShowStaleHint));
    }

    partial void OnIsQuickRatesOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public ObservableCollection<PaymentRow> PaymentRows { get; } = [];
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<PayMethodOption> PayMethods { get; } = [];

    private decimal RateOf(string? code) => code == _baseCurrency ? 1m : code is null ? 0m : _rates.GetValueOrDefault(code, 0m);
    private decimal PaidBonusBase => IsMulticurrency ? PaymentRows.Where(r => r.Method.Key == "bonus").Sum(r => r.AmountBase) : PaidBonus;

    public decimal SubTotal => CartItems.Sum(i => i.LineTotal);
    public decimal TotalAmount => Math.Max(0, SubTotal - DiscountAmount - AutoDiscountAmount);
    [ObservableProperty] private decimal _autoDiscountAmount;
    public bool HasAutoDiscount => AutoDiscountAmount > 0;
    public decimal TotalPaid => IsMulticurrency ? PaymentRows.Sum(r => r.AmountBase) : PaidCash + PaidCard + PaidBonus;
    public decimal ChangeAmount => TotalPaid > TotalAmount ? TotalPaid - TotalAmount : 0;
    public decimal DebtAmount => TotalPaid < TotalAmount ? TotalAmount - TotalPaid : 0;
    public bool IsCartEmpty => CartItems.Count == 0;
    public bool CanOverridePrice => _auth.HasPermission("sales.priceOverride");
    [ObservableProperty] private bool _canPrepack;

    public decimal CustomerDebt => SelectedCustomer?.DebtBalance ?? 0;
    public bool HasCreditLimit => (SelectedCustomer?.CreditLimit ?? 0) > 0;
    public decimal RemainingCredit => Math.Max(0, (SelectedCustomer?.CreditLimit ?? 0) - CustomerDebt);
    public bool IsOverCreditLimit => HasCreditLimit && CustomerDebt + DebtAmount > SelectedCustomer!.CreditLimit;
    public bool CustomerIsDebtor => CustomerDebt > 0;
    public bool CustomerIsCreditor => CustomerDebt < 0;
    public decimal CustomerBalanceAbs => Math.Abs(CustomerDebt);

    private bool _allowDebtSales = true;
    private bool _requireDebtDueDate = true;
    [ObservableProperty] private bool _allowCustomerCredit;
    private bool? _excessOverride;

    public bool CanCreditExcess => AllowCustomerCredit && SelectedCustomer is not null;
    public bool ExcessToCredit => ChangeAmount > 0 && CanCreditExcess && (_excessOverride ?? true);
    public bool ShowChange => ChangeAmount > 0 && !ExcessToCredit;
    public decimal CreditAmount => ExcessToCredit ? ChangeAmount : 0;
    public bool DebtCoveredByCredit => DebtAmount > 0 && DebtAmount <= Math.Max(0, -CustomerDebt);
    public bool ShowDebt => DebtAmount > 0;
    public bool ShowDebtDueDate => DebtAmount > 0 && !DebtCoveredByCredit;

    public string DebtDisplay
    {
        get
        {
            var currency = SelectedDebtCurrency ?? _baseCurrency;
            var rate = RateOf(currency);
            if (currency == _baseCurrency || rate <= 0) return DebtAmount.ToString("N0");
            return $"{Math.Round(DebtAmount / rate, 2):N2} {currency}";
        }
    }

    [RelayCommand]
    private void ToggleExcessTarget()
    {
        if (!CanCreditExcess) return;
        _excessOverride = !ExcessToCredit;
        NotifyExcess();
    }

    private void NotifyExcess()
    {
        OnPropertyChanged(nameof(CanCreditExcess));
        OnPropertyChanged(nameof(ExcessToCredit));
        OnPropertyChanged(nameof(ShowChange));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(DebtCoveredByCredit));
        OnPropertyChanged(nameof(ShowDebt));
        OnPropertyChanged(nameof(ShowDebtDueDate));
        OnPropertyChanged(nameof(DebtDisplay));
    }

    partial void OnAllowCustomerCreditChanged(bool value) => NotifyExcess();

    public SalesViewModel(ISalesApi salesApi, IStocksApi stocksApi, ICustomersApi customersApi,
        IProductsApi productsApi, ICategoriesApi categoriesApi, IReceiptApi receiptApi, IBarcodesApi barcodesApi, BranchContextService branch,
        QuickProductViewModel quickProduct, IToastService toast, IBusyService busy, IHeldSaleStore heldStore, IPrinterService printer, IScannedCodeParser scannedCodeParser, AuthService auth,
        IBusinessApi businessApi, IRatesApi ratesApi, IOrderingApi orderingApi, PosHandoffService handoff, ISettingsApi settingsApi,
        IPrepacksApi prepacksApi, PrepackViewModel prepack, IFeaturesApi featuresApi, ReferenceCache cache,
        ISuppliersApi suppliersApi, ISuppliesApi suppliesApi, QueueHubService queueHub, IShiftsApi shiftsApi)
    {
        _shiftsApi = shiftsApi;
        _cache = cache;
        _suppliersApi = suppliersApi;
        _suppliesApi = suppliesApi;
        _prepacksApi = prepacksApi;
        Prepack = prepack;
        _featuresApi = featuresApi;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _orderingApi = orderingApi;
        _handoff = handoff;
        _queueHub = queueHub;
        _settingsApi = settingsApi;
        PaymentRows.CollectionChanged += (_, _) => NotifyTotals();
        _salesApi = salesApi;
        _stocksApi = stocksApi;
        _customersApi = customersApi;
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _receiptApi = receiptApi;
        _barcodesApi = barcodesApi;
        Branch = branch;
        QuickProduct = quickProduct;
        QuickProduct.Created += OnQuickProductCreated;
        QuickProduct.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(QuickProductViewModel.IsOpen)) OnPropertyChanged(nameof(IsModalOpen)); };
        Prepack.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PrepackViewModel.IsOpen)) OnPropertyChanged(nameof(IsModalOpen)); };
        _toast = toast;
        _busy = busy;
        _heldStore = heldStore;
        _printer = printer;
        _scannedCodeParser = scannedCodeParser;
        _auth = auth;

        CartItems.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (CartItem it in e.NewItems) it.PropertyChanged += OnCartItemChanged;
            NotifyTotals();
        };
        Branch.PropertyChanged += OnBranchChanged;
        _auth.LoggedOut += ClearCart;
        LoadHeldSales();
    }

    public event Action? ScanFocusRequested;

    private IReadOnlyList<PageShortcut>? _pageShortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _pageShortcuts ??=
    [
        new(Key.F2, KeyModifiers.None, "shortcut_scan_focus", () => ScanFocusRequested?.Invoke(), WorksInText: true),
        new(Key.F4, KeyModifiers.None, "shortcut_pay_exact", () => PayExactCommand.Execute(null), WorksInText: true),
        new(Key.F6, KeyModifiers.None, "shortcut_hold", () => HoldSaleCommand.Execute(null), WorksInText: true),
        new(Key.F9, KeyModifiers.None, "shortcut_complete", () => CompleteSaleCommand.Execute(null), WorksInText: true),
        new(Key.Escape, KeyModifiers.None, "shortcut_close_clear", HandleEscape, WorksInText: true),
    ];

    private void HandleEscape()
    {
        if (IsQuickRatesOpen) { IsQuickRatesOpen = false; return; }
        if (IsReceiptOpen) { IsReceiptOpen = false; return; }
        if (IsReceiveOpen) { IsReceiveOpen = false; return; }
        if (IsProductDetailOpen) { IsProductDetailOpen = false; return; }
        if (IsCustomerPanelOpen) { IsCustomerPanelOpen = false; return; }
        if (IsHeldPanelOpen) { IsHeldPanelOpen = false; return; }
        if (IsQueuePanelOpen) { IsQueuePanelOpen = false; return; }
        ClearCart();
    }

    private void OnCartItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartItem.Quantity) or nameof(CartItem.LineTotal))
            NotifyTotals();
    }

    private void LoadHeldSales()
    {
        HeldSales.Clear();
        foreach (var held in _heldStore.Load()) HeldSales.Add(held);
    }

    private ISettingsApi _settingsApi = null!;
    private ReferenceCache _cache = null!;
    private int _staleRateDays = 3;
    private bool _supplierRequired;

    private async Task LoadClientPolicyAsync()
    {
        try
        {
            var policyTask = _cache.GetAsync(CacheKeys.SalesPolicy, _settingsApi.GetSalesPolicyAsync);
            var receiptTask = _cache.GetAsync(CacheKeys.Receipt, _settingsApi.GetReceiptAsync);
            var policy = await policyTask;
            _staleRateDays = policy.StaleRateDays;
            _allowDebtSales = policy.AllowDebtSales;
            _requireDebtDueDate = policy.RequireDebtDueDate;
            _supplierRequired = policy.RequireSupplier;
            ShiftRequired = policy.ShiftPolicy != "Off";
            AllowCustomerCredit = policy.AllowCustomerCredit;
            var receipt = await receiptTask;
            _printer.ReceiptOptions = new ReceiptPrintOptions(receipt.HeaderText, receipt.FooterText, receipt.PaperWidth);
        }
        catch { }
    }

    public async Task LoadAsync()
    {
        var policyTask = LoadClientPolicyAsync();
        await Task.WhenAll(
            policyTask,
            LoadMulticurrencyAsync(policyTask),
            LoadCategoriesAsync(),
            LoadProductsAsync(),
            LoadPrepackAccessAsync(),
            LoadReceiveAccessAsync(),
            LoadQueueAccessAsync(),
            LoadShiftAsync());

        if (_handoff.PendingCartCode is { } pending)
        {
            _handoff.PendingCartCode = null;
            await TryLoadCartAsync(pending);
        }
    }

    private async Task<bool> TryLoadCartAsync(string code)
    {
        try
        {
            var cart = await _orderingApi.GetByCodeAsync(code);
            if (cart.Status is "CheckedOut" or "Cancelled")
            {
                _toast.Warning(L["cart_already_done"]);
                return true;
            }
            if (CartItems.Count > 0)
            {
                _toast.Warning(L["cart_not_empty"]);
                return true;
            }

            foreach (var item in cart.Items)
            {
                var stock = Products.FirstOrDefault(p => p.VariantId == item.VariantId);
                AddToCart(item.VariantId, item.ProductName, item.UnitPrice, item.Quantity, stock?.Quantity);
            }

            if (cart.CustomerId is { } customerId)
            {
                try { SelectedCustomer = await _customersApi.GetByIdAsync(customerId); }
                catch { }
            }

            if (cart.Status == "Open")
            {
                try { await _orderingApi.UpdateStatusAsync(code, new Cartex.Shared.Models.Ordering.UpdateCartStatusRequest("Confirmed")); }
                catch { }
            }

            _activeCartCode = code;
            _toast.Success(L["cart_loaded"]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadMulticurrencyAsync(Task policyTask)
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            if (!IsMulticurrency) return;

            var currencies = await _ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            _foreignCurrencies = currencies.Where(c => !c.IsBase).OrderBy(c => c.Code).ToList();
            _defaultCurrency = currencies.FirstOrDefault(c => c.IsDefault)?.Code ?? _baseCurrency;
            _rates.Clear();
            foreach (var c in _foreignCurrencies.Where(c => c.Rate is > 0)) _rates[c.Code] = c.Rate!.Value;
            if (_defaultCurrency != _baseCurrency && !_rates.ContainsKey(_defaultCurrency)) _defaultCurrency = _baseCurrency;
            await policyTask;
            var limit = DateTime.Now.AddDays(-_staleRateDays);
            HasStaleRate = _foreignCurrencies.Any(c => c.RateAt is null || c.RateAt < limit);

            var codes = new List<string> { _baseCurrency };
            codes.AddRange(_foreignCurrencies.Where(c => c.Rate is > 0).Select(c => c.Code));
            if (!codes.SequenceEqual(Currencies))
            {
                Currencies.Clear();
                foreach (var code in codes) Currencies.Add(code);
                foreach (var row in PaymentRows)
                {
                    if (!codes.Contains(row.Currency)) row.Currency = _defaultCurrency;
                    else row.RefreshCurrency();
                }
            }

            var methods = new List<PayMethodOption> { new("cash", L["cash"]), new("card", L["card"]), new("bonus", L["bonus"]) };
            if (!methods.SequenceEqual(PayMethods))
            {
                var methodKeys = PaymentRows.Select(r => r.Method.Key).ToList();
                PayMethods.Clear();
                foreach (var m in methods) PayMethods.Add(m);
                for (var i = 0; i < PaymentRows.Count; i++)
                    PaymentRows[i].Method = PayMethods.FirstOrDefault(m => m.Key == methodKeys[i]) ?? PayMethods[0];
            }

            if (SelectedDebtCurrency is null || !codes.Contains(SelectedDebtCurrency))
                SelectedDebtCurrency = _baseCurrency;
            if (PaymentRows.Count == 0)
                AddPayment();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void AddPayment()
    {
        var row = new PaymentRow(_defaultCurrency, PayMethods.FirstOrDefault() ?? new PayMethodOption("cash", L["cash"]),
            Currencies, PayMethods, NotifyTotals, RateOf, _baseCurrency);
        PaymentRows.Add(row);
    }

    [RelayCommand]
    private void RemovePayment(PaymentRow row) => PaymentRows.Remove(row);

    [ObservableProperty] private bool _shiftRequired;
    [ObservableProperty] private bool _shiftOpen;
    [ObservableProperty] private decimal _shiftOpeningFloat;

    private async Task LoadShiftAsync()
    {
        try { ShiftOpen = await _shiftsApi.GetCurrentAsync() is not null; }
        catch { }
    }

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _shiftsApi.OpenAsync(new OpenShiftRequest(ShiftOpeningFloat, null));
            ShiftOpeningFloat = 0;
            ShiftOpen = true;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenQuickRates()
    {
        if (!CanManageRates) return;
        QuickRates.Clear();
        var limit = DateTime.Now.AddDays(-_staleRateDays);
        foreach (var c in _foreignCurrencies)
            QuickRates.Add(new QuickRateItem(c, c.RateAt is null || c.RateAt < limit));
        IsQuickRatesOpen = true;
    }

    [RelayCommand]
    private void CloseQuickRates() => IsQuickRatesOpen = false;

    [RelayCommand]
    private async Task SaveQuickRatesAsync()
    {
        var changed = QuickRates.Where(q => q.NewRate > 0 && q.NewRate != q.CurrentRate).ToList();
        if (changed.Count == 0) { IsQuickRatesOpen = false; return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                foreach (var q in changed)
                    await _ratesApi.SetAsync(new SetRateRequest(q.Code, q.NewRate));
            _cache.Invalidate(CacheKeys.Rates);
            IsQuickRatesOpen = false;
            _toast.Success(L["success"]);
            await LoadMulticurrencyAsync(Task.CompletedTask);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync());
            _allCategories.Clear();
            _allCategories.AddRange(categories);
            _selectedCategoryId = null;
            BuildChips();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void BuildChips()
    {
        NavCategories.Clear();
        NavCategories.Add(new CategoryChip(null, L["all"]) { IsSelected = _selectedCategoryId is null });
        foreach (var c in _allCategories.Where(c => c.ParentId is null).OrderBy(c => c.Name))
            NavCategories.Add(new CategoryChip(c.Id, c.Name) { IsSelected = c.Id == _selectedCategoryId });
        BuildSubChips(_selectedCategoryId);
    }

    private void BuildSubChips(long? rootId)
    {
        SubCategories.Clear();
        if (rootId is not null)
        {
            var children = _allCategories.Where(c => c.ParentId == rootId).OrderBy(c => c.Name).ToList();
            if (children.Count > 0)
            {
                SubCategories.Add(new CategoryChip(rootId, L["all"]) { IsSelected = true });
                foreach (var c in children)
                    SubCategories.Add(new CategoryChip(c.Id, c.Name));
            }
        }
        HasSubCategories = SubCategories.Count > 0;
    }

    [RelayCommand]
    private async Task SelectCategory(CategoryChip chip)
    {
        _selectedCategoryId = chip.Id;
        foreach (var c in NavCategories)
            c.IsSelected = c.Id == _selectedCategoryId;
        BuildSubChips(chip.Id);
        await LoadProductsAsync();
    }

    [RelayCommand]
    private async Task SelectSubCategory(CategoryChip chip)
    {
        _selectedCategoryId = chip.Id;
        foreach (var c in SubCategories)
            c.IsSelected = ReferenceEquals(c, chip);
        await LoadProductsAsync();
    }

    private void OnBranchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchContextService.SelectedWarehouse))
            _ = LoadProductsAsync();
    }

    private void NotifyTotals()
    {
        if (_discountByPercent && DiscountPercent > 0 && !_syncingDiscount)
        {
            _syncingDiscount = true;
            DiscountAmount = SubTotal > 0 ? Math.Round(SubTotal * DiscountPercent / 100, 2) : 0;
            _syncingDiscount = false;
        }
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalPaid));
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(DebtAmount));
        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(IsOverCreditLimit));
        NotifyExcess();
        SchedulePreview();
    }

    private CancellationTokenSource? _previewCts;

    private void SchedulePreview()
    {
        _previewCts?.Cancel();
        var cts = _previewCts = new CancellationTokenSource();
        _ = PreviewAsync(cts.Token);
    }

    private async Task PreviewAsync(CancellationToken token)
    {
        try { await Task.Delay(250, token); } catch { return; }
        if (token.IsCancellationRequested) return;

        if (CartItems.Count == 0 || IsOfflineMode)
        {
            SetAutoDiscount(0);
            return;
        }
        try
        {
            var total = await FetchPreviewTotalAsync();
            if (!token.IsCancellationRequested)
                SetAutoDiscount(total);
        }
        catch { }
    }

    private async Task RefreshPreviewNowAsync()
    {
        _previewCts?.Cancel();
        if (CartItems.Count == 0 || IsOfflineMode)
        {
            SetAutoDiscount(0);
            return;
        }
        try { SetAutoDiscount(await FetchPreviewTotalAsync()); }
        catch { }
    }

    private async Task<decimal> FetchPreviewTotalAsync()
    {
        var items = CartItems
            .Select(c => new PreviewDiscountItemRequest(c.VariantId, c.Quantity, c.UnitPrice))
            .ToList();
        var result = await ServiceLocator.Resolve<ILoyaltyApi>()
            .PreviewDiscountAsync(new PreviewDiscountRequest(SelectedCustomer?.Id, items));
        return result.Total;
    }

    private void SetAutoDiscount(decimal value)
    {
        if (AutoDiscountAmount == value) return;
        AutoDiscountAmount = value;
        OnPropertyChanged(nameof(HasAutoDiscount));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(DebtAmount));
        OnPropertyChanged(nameof(IsOverCreditLimit));
        NotifyExcess();
    }

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(CustomerDebt));
        OnPropertyChanged(nameof(HasCreditLimit));
        OnPropertyChanged(nameof(RemainingCredit));
        OnPropertyChanged(nameof(IsOverCreditLimit));
        OnPropertyChanged(nameof(CustomerIsDebtor));
        OnPropertyChanged(nameof(CustomerIsCreditor));
        OnPropertyChanged(nameof(CustomerBalanceAbs));
        NotifyExcess();
        SchedulePreview();
    }

    partial void OnPaidCashChanged(decimal value) => NotifyTotals();
    partial void OnPaidCardChanged(decimal value) => NotifyTotals();
    partial void OnPaidBonusChanged(decimal value) => NotifyTotals();

    partial void OnDiscountAmountChanged(decimal value)
    {
        if (!_syncingDiscount)
        {
            _discountByPercent = false;
            _syncingDiscount = true;
            DiscountPercent = SubTotal > 0 ? Math.Round(value / SubTotal * 100, 2) : 0;
            _syncingDiscount = false;
        }
        NotifyTotals();
    }

    partial void OnDiscountPercentChanged(decimal value)
    {
        if (_syncingDiscount) return;
        _discountByPercent = true;
        _syncingDiscount = true;
        DiscountAmount = SubTotal > 0 ? Math.Round(SubTotal * value / 100, 2) : 0;
        _syncingDiscount = false;
        NotifyTotals();
    }
    private CancellationTokenSource? _productSearchCts;

    partial void OnSearchTextChanged(string value)
    {
        _productSearchCts?.Cancel();
        var cts = _productSearchCts = new CancellationTokenSource();
        _ = DebouncedProductsAsync(cts.Token);
    }

    private async Task DebouncedProductsAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (!token.IsCancellationRequested) await LoadProductsAsync();
    }
    private Task LoadProductsAsync() => LoadProductsPageAsync(reset: true);

    private static bool IsOfflineMode =>
        ServiceLocator.Resolve<OfflineSyncService>().IsEnabled &&
        !ServiceLocator.Resolve<ConnectivityService>().IsOnline;

    private static OfflineStore Offline => ServiceLocator.Resolve<OfflineStore>();

    private static CustomerDto ToCustomerDto(OfflineCustomer c) =>
        new(c.Id, c.FullName, null, null, c.Phone, null, c.CardBarcode, c.DiscountPct, 0, c.DebtBalance, c.CreditLimit);

    private int _productsGeneration;
    private bool _productsResetInFlight;

    private async Task LoadProductsPageAsync(bool reset)
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;
        if (!reset && _productsResetInFlight) return;

        if (IsOfflineMode)
        {
            var term = SearchText.Trim();
            var cached = await Offline.SearchProductsAsync(string.IsNullOrEmpty(term) ? null : term, 300);
            Products.Clear();
            foreach (var p in cached)
                Products.Add(new StockOnHandDto(p.VariantId, p.ProductName, null, p.CategoryName, p.UnitName, "", p.Quantity, p.SellingPrice, null));
            ProductsTotal = Products.Count;
            OnPropertyChanged(nameof(HasMoreProducts));
            return;
        }

        var generation = reset ? ++_productsGeneration : _productsGeneration;
        var targetPage = reset ? 1 : _productsPage + 1;
        if (reset) _productsResetInFlight = true;
        try
        {
            var search = SearchText.Trim();
            var page = await _stocksApi.GetOnHandAsync(warehouseId.Value, _selectedCategoryId,
                string.IsNullOrEmpty(search) ? null : search, targetPage, PosPageSize, forSale: true);
            if (generation != _productsGeneration) return;
            _productsPage = targetPage;
            if (reset) Products.Clear();
            foreach (var s in page.Items) Products.Add(s);
            ProductsTotal = page.TotalCount;
            OnPropertyChanged(nameof(HasMoreProducts));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally
        {
            if (reset && generation == _productsGeneration) _productsResetInFlight = false;
        }
    }

    [RelayCommand]
    private Task LoadMoreProducts() => LoadProductsPageAsync(reset: false);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ScanAsync()
    {
        var code = SearchText.Trim();
        if (string.IsNullOrEmpty(code)) return;

        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null)
        {
            _toast.Warning(L["select_warehouse"]);
            return;
        }

        SearchText = string.Empty;
        var visibleProducts = Products.ToList();

        if (code.Length == 32 && code.All(char.IsAsciiHexDigit))
        {
            if (await TryLoadCartAsync(code))
                return;
        }

        var scanned = _scannedCodeParser.Parse(code);

        if (IsOfflineMode)
        {
            var cachedBarcode = await Offline.GetBarcodeAsync(scanned.Code);
            if (cachedBarcode is not null && await Offline.GetProductAsync(cachedBarcode.VariantId) is { } cachedProduct)
            {
                var qty = scanned.Type == ScannedCodeType.Weighted && scanned.Weight is { } w
                    ? w
                    : cachedBarcode.PackQty > 1 ? cachedBarcode.PackQty : 1;
                AddToCart(cachedProduct.VariantId, cachedProduct.ProductName, cachedProduct.SellingPrice, qty, cachedProduct.Quantity);
                return;
            }
            if (await Offline.GetCustomerByCardAsync(code) is { } cachedCustomer)
            {
                SelectedCustomer = ToCustomerDto(cachedCustomer);
                _toast.Success(cachedCustomer.FullName);
                return;
            }
            _toast.Warning(L["offline_pos_limited"]);
            return;
        }

        try
        {
            var product = await _productsApi.GetByBarcodeAsync(scanned.Code, warehouseId.Value, forSale: true);
            var quantity = scanned.Type == ScannedCodeType.Weighted && scanned.Weight is { } weight
                ? weight
                : product.PackQty > 1 ? product.PackQty : 1;
            AddToCart(product.VariantId, product.ProductName, product.SellingPrice, quantity, product.OnHand);
            return;
        }
        catch (ApiException)
        {
        }

        if (code.StartsWith("PP"))
        {
            try
            {
                var prepack = await _prepacksApi.GetByCodeAsync(code, warehouseId.Value);
                AddPrepackToCart(prepack);
                return;
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                _toast.Error(ApiErrors.Describe(ex));
                return;
            }
            catch (ApiException)
            {
            }
        }

        try
        {
            var customer = await _customersApi.GetByCardAsync(code);
            if (customer is not null)
            {
                SelectedCustomer = customer;
                _toast.Success(customer.FullName);
                return;
            }
        }
        catch (ApiException)
        {
        }

        if (visibleProducts.Count > 0)
        {
            var first = visibleProducts[0];
            AddToCart(first.VariantId, first.ProductName, first.SellingPrice, available: first.Quantity);
            return;
        }

        await QuickProduct.OpenCommand.ExecuteAsync(null);
        QuickProduct.Barcode = code;
    }

    private void OnQuickProductCreated(long variantId, string name) =>
        AddToCart(variantId, name, QuickProduct.SellingPrice ?? 0);

    [RelayCommand]
    private void AddStockToCart(StockOnHandDto stock) =>
        AddToCart(stock.VariantId, stock.ProductName, stock.SellingPrice, available: stock.Quantity);

    [RelayCommand]
    public void ShowProductDetail(StockOnHandDto product)
    {
        DetailProduct = product;
        DetailBarcodes = string.Empty;
        _detailRequestedQty = CartItems.Where(c => c.VariantId == product.VariantId && !c.IsPrepack).Sum(c => c.Quantity);
        IsReceiveOpen = false;
        IsProductDetailOpen = true;
        _ = LoadDetailBarcodesAsync(product.VariantId);
    }

    [RelayCommand]
    private void ShowCartDetail(CartItem item) =>
        ShowProductDetail(Products.FirstOrDefault(p => p.VariantId == item.VariantId)
            ?? new StockOnHandDto(item.VariantId, item.ProductName, null, null, string.Empty, string.Empty, item.Available ?? 0, item.UnitPrice, null));

    private async Task LoadDetailBarcodesAsync(long variantId)
    {
        try
        {
            var codes = await _barcodesApi.GetByVariantAsync(variantId);
            DetailBarcodes = string.Join(", ", codes.Select(b => b.Code));
        }
        catch { }
    }

    [RelayCommand]
    private void CloseProductDetail() => IsProductDetailOpen = false;

    [RelayCommand]
    private void AddDetailToCart()
    {
        if (DetailProduct is not null) AddStockToCart(DetailProduct);
        IsProductDetailOpen = false;
    }

    private async Task LoadReceiveAccessAsync()
    {
        if (!_auth.HasPermission("supplies.manage"))
        {
            CanReceiveStock = false;
            return;
        }
        try
        {
            var enabled = await _cache.GetAsync(CacheKeys.Features, _featuresApi.GetEnabledAsync);
            CanReceiveStock = enabled.Contains("supplies");
        }
        catch { CanReceiveStock = true; }
    }

    private async Task EnsureSuppliersAsync()
    {
        SupplierOptions.Clear();
        if (!_supplierRequired) SupplierOptions.Add(new IdOption(null, L["none"]));
        try
        {
            var suppliers = await _cache.GetAsync(CacheKeys.Suppliers, () => _suppliersApi.GetAllAsync());
            foreach (var s in suppliers) SupplierOptions.Add(new IdOption(s.Id, s.Name));
        }
        catch { }
        ReceiveSupplier = SupplierOptions.FirstOrDefault(o => o.Id == _lastSupplierId) ?? SupplierOptions.FirstOrDefault();
        HasNoSuppliers = !SupplierOptions.Any(o => o.Id is not null);
    }

    [RelayCommand]
    private async Task OpenReceive()
    {
        if (IsOfflineMode) { _toast.Warning(L["offline_pos_limited"]); return; }
        ReceiveQuantity = Math.Max(0, _detailRequestedQty - (DetailProduct?.Quantity ?? 0));
        ReceivePurchasePrice = 0;
        _lastPurchasePrice = 0;
        ReceiveSellingPrice = DetailProduct?.SellingPrice ?? 0;
        ReceiveExpiry = null;
        ReceiveCurrency = _baseCurrency;
        IsReceiveOpen = true;
        await EnsureSuppliersAsync();
        if (DetailProduct is not { } product || Branch.CurrentWarehouseId is not { } warehouseId) return;
        try
        {
            var info = await _productsApi.GetVariantPriceInfoAsync(product.VariantId, warehouseId);
            _lastPurchasePrice = info.LastPurchasePrice ?? 0;
            if (ReceiveCurrency == _baseCurrency) ReceivePurchasePrice = _lastPurchasePrice;
        }
        catch { }
    }

    partial void OnReceiveCurrencyChanged(string? value)
    {
        if (!IsReceiveOpen) return;
        var isBase = value == _baseCurrency;
        ReceivePurchasePrice = isBase ? _lastPurchasePrice : 0;
        ReceiveSellingPrice = isBase ? DetailProduct?.SellingPrice ?? 0 : 0;
    }

    [RelayCommand]
    private void CloseReceive() => IsReceiveOpen = false;

    [RelayCommand]
    private async Task ReceiveSupplyAsync()
    {
        if (DetailProduct is not { } product) return;
        if (Branch.CurrentWarehouseId is not { } warehouseId) { _toast.Warning(L["select_warehouse"]); return; }
        if (ReceiveQuantity <= 0) return;
        if (_supplierRequired && ReceiveSupplier?.Id is null) { _toast.Warning(L["err_select_supplier"]); return; }
        var supplierId = ReceiveSupplier?.Id;
        var currency = IsMulticurrency && ReceiveCurrency != _baseCurrency ? ReceiveCurrency : null;
        var sellingPrice = ReceiveSellingPrice > 0 && (currency is not null || ReceiveSellingPrice != product.SellingPrice)
            ? ReceiveSellingPrice : (decimal?)null;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _suppliesApi.CreateAsync(new CreateSupplyRequest(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                    [new CreateSupplyItemRequest(product.VariantId, ReceiveQuantity, ReceivePurchasePrice,
                        ReceiveExpiry is { } exp ? DateOnly.FromDateTime(exp) : null, null, sellingPrice)],
                    Currency: currency));

            _lastSupplierId = supplierId;
            _cache.Invalidate(CacheKeys.Suppliers);
            var updated = product with
            {
                Quantity = product.Quantity + ReceiveQuantity,
                SellingPrice = currency is null && sellingPrice is { } sp ? sp : product.SellingPrice
            };
            var index = Products.IndexOf(product);
            if (index >= 0) Products[index] = updated;
            DetailProduct = updated;
            foreach (var item in CartItems.Where(c => c.VariantId == updated.VariantId && !c.IsPrepack))
                item.Available = updated.Quantity;
            IsReceiveOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadPrepackAccessAsync()
    {
        if (!_auth.HasPermission("sales.prepack"))
        {
            CanPrepack = false;
            return;
        }
        try
        {
            var enabled = await _cache.GetAsync(CacheKeys.Features, _featuresApi.GetEnabledAsync);
            CanPrepack = enabled.Contains("prepack");
        }
        catch { CanPrepack = false; }
    }

    private void AddPrepackToCart(PrepackLookupDto prepack)
    {
        if (CartItems.Any(c => c.PrepackId == prepack.PrepackId))
        {
            _toast.Warning(L["prepack_in_cart"]);
            return;
        }

        CartItems.Add(new CartItem
        {
            VariantId = prepack.VariantId,
            PrepackId = prepack.PrepackId,
            ProductName = $"{prepack.ProductName} ({prepack.Quantity:0.###} {prepack.UnitName})",
            OriginalPrice = prepack.UnitPrice,
            UnitPrice = prepack.UnitPrice,
            Quantity = prepack.Quantity
        });
        NotifyTotals();
    }

    [RelayCommand]
    private async Task OpenPrepack()
    {
        if (Branch.CurrentWarehouseId is not { } warehouseId)
        {
            _toast.Warning(L["select_warehouse"]);
            return;
        }
        await Prepack.OpenAsync(warehouseId);
    }

    private void AddToCart(long variantId, string name, decimal price, decimal quantity = 1, decimal? available = null)
    {
        var existing = CartItems.FirstOrDefault(c => c.VariantId == variantId && !c.IsPrepack);
        if (existing is not null)
        {
            if (available is not null) existing.Available = available;
            existing.Quantity += quantity;
            NotifyTotals();
            return;
        }

        CartItems.Add(new CartItem
        {
            VariantId = variantId,
            ProductName = name,
            OriginalPrice = price,
            UnitPrice = price,
            Quantity = quantity,
            Available = available
        });
        NotifyTotals();
    }

    [RelayCommand]
    private void Increment(CartItem item)
    {
        if (item.IsPrepack) return;
        item.Quantity += 1;
        NotifyTotals();
    }

    [RelayCommand]
    private void Decrement(CartItem item)
    {
        if (item.IsPrepack) { CartItems.Remove(item); NotifyTotals(); return; }
        if (item.Quantity <= 1) CartItems.Remove(item);
        else item.Quantity -= 1;
        NotifyTotals();
    }

    [RelayCommand]
    private void RemoveItem(CartItem item) { CartItems.Remove(item); NotifyTotals(); }

    [RelayCommand]
    private void PayExact()
    {
        if (IsMulticurrency)
        {
            var remaining = TotalAmount - TotalPaid;
            if (remaining <= 0) return;
            var row = PaymentRows.FirstOrDefault(r => r.Currency == _baseCurrency && r.Method.Key == "cash")
                ?? PaymentRows.FirstOrDefault(r => r.Amount == 0);
            if (row is null) { AddPayment(); row = PaymentRows[^1]; }
            row.Method = PayMethods.FirstOrDefault(m => m.Key == "cash") ?? row.Method;
            row.Currency = _baseCurrency;
            row.Amount += remaining;
            return;
        }
        PaidCash = Math.Max(0, TotalAmount - PaidCard - PaidBonus);
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        _activeCartCode = null;
        DebtDueDate = null;
        DueDateMissing = false;
        PaidCash = PaidCard = PaidBonus = DiscountAmount = 0;
        _syncingDiscount = true;
        DiscountPercent = 0;
        _syncingDiscount = false;
        _discountByPercent = false;
        _saleIdempotencyKey = null;
        _excessOverride = null;
        if (IsMulticurrency)
        {
            PaymentRows.Clear();
            AddPayment();
            SelectedDebtCurrency = _baseCurrency;
        }
        SelectedCustomer = null;
        NotifyTotals();
    }

    [ObservableProperty] private bool _isAddingCustomer;
    [ObservableProperty] private bool _bonusVisible;
    [ObservableProperty] private string _newCustomerName = string.Empty;
    [ObservableProperty] private string _newCustomerLastName = string.Empty;
    [ObservableProperty] private string _newCustomerAddress = string.Empty;
    [ObservableProperty] private string _newCustomerPhone = string.Empty;
    [ObservableProperty] private string _newCustomerEmail = string.Empty;
    [ObservableProperty] private string _newCustomerCard = string.Empty;
    [ObservableProperty] private decimal _newCustomerDiscount;
    [ObservableProperty] private decimal _newCustomerCreditLimit;
    [ObservableProperty] private decimal _newCustomerOpening;
    [ObservableProperty] private int _newCustomerOpeningKindIndex;
    [ObservableProperty] private string? _newCustomerOpeningCurrency;

    public ObservableCollection<string> OpeningKinds { get; } = [];
    private CancellationTokenSource? _customerSearchCts;

    partial void OnCustomerSearchChanged(string value)
    {
        _customerSearchCts?.Cancel();
        var cts = _customerSearchCts = new CancellationTokenSource();
        _ = DebouncedCustomerSearchAsync(cts.Token);
    }

    private async Task DebouncedCustomerSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (!token.IsCancellationRequested) await SearchCustomerAsync();
    }

    [RelayCommand]
    private async Task SearchCustomerAsync()
    {
        var query = CustomerSearch.Trim();
        try
        {
            if (IsOfflineMode)
            {
                var cached = await Offline.SearchCustomersAsync(string.IsNullOrEmpty(query) ? null : query, 30);
                CustomerResults.Clear();
                foreach (var c in cached)
                    CustomerResults.Add(ToCustomerDto(c));
                return;
            }
            var customers = await _customersApi.QueryAsync(QueryRequest.Create().Page(1, 30).Search(query).Build());
            CustomerResults.Clear();
            foreach (var c in customers.Content ?? [])
                CustomerResults.Add(c);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void SelectCustomer(CustomerDto customer)
    {
        SelectedCustomer = customer;
        IsCustomerPanelOpen = false;
    }

    [RelayCommand]
    private void ClearCustomer() => SelectedCustomer = null;

    [RelayCommand]
    private void ToggleCustomerPanel()
    {
        IsCustomerPanelOpen = !IsCustomerPanelOpen;
        IsAddingCustomer = false;
        if (IsCustomerPanelOpen && CustomerResults.Count == 0)
            _ = SearchCustomerAsync();
    }

    [RelayCommand]
    private void ToggleBonus() => BonusVisible = !BonusVisible;

    [RelayCommand]
    private void ShowAddCustomer()
    {
        NewCustomerName = CustomerSearch.Trim();
        NewCustomerLastName = string.Empty;
        NewCustomerAddress = string.Empty;
        NewCustomerPhone = string.Empty;
        NewCustomerEmail = string.Empty;
        NewCustomerCard = string.Empty;
        NewCustomerDiscount = 0;
        NewCustomerCreditLimit = 0;
        NewCustomerOpening = 0;
        NewCustomerOpeningKindIndex = 0;
        OpeningKinds.Clear();
        OpeningKinds.Add(L["opening_kind_debt"]);
        OpeningKinds.Add(L["opening_kind_credit"]);
        NewCustomerOpeningCurrency = _baseCurrency;
        IsAddingCustomer = true;
    }

    [RelayCommand]
    private void CancelAddCustomer() => IsAddingCustomer = false;

    [RelayCommand]
    private async Task SaveNewCustomer()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName) || string.IsNullOrWhiteSpace(NewCustomerPhone)) { _toast.Warning(L["required_fields_hint"]); return; }
        var phone = string.IsNullOrWhiteSpace(NewCustomerPhone) ? null : NewCustomerPhone.Trim();
        var email = string.IsNullOrWhiteSpace(NewCustomerEmail) ? null : NewCustomerEmail.Trim();
        var card = string.IsNullOrWhiteSpace(NewCustomerCard) ? null : NewCustomerCard.Trim();
        var lastName = string.IsNullOrWhiteSpace(NewCustomerLastName) ? null : NewCustomerLastName.Trim();
        var address = string.IsNullOrWhiteSpace(NewCustomerAddress) ? null : NewCustomerAddress.Trim();
        try
        {
            long id;
            using (_busy.Begin(L["loading"]))
            {
                var opening = NewCustomerOpeningKindIndex == 1 ? -NewCustomerOpening : NewCustomerOpening;
                id = await _customersApi.CreateAsync(new CreateCustomerRequest(NewCustomerName.Trim(), phone, card, NewCustomerDiscount, email, lastName, address, NewCustomerCreditLimit,
                    OpeningBalance: opening, OpeningCurrency: IsMulticurrency ? NewCustomerOpeningCurrency : null));
                SelectedCustomer = await _customersApi.GetByIdAsync(id);
            }
            IsAddingCustomer = false;
            IsCustomerPanelOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static int HoldNumber(HeldSale h) =>
        int.TryParse(new string(h.Label.SkipWhile(c => c != '#').Skip(1).TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0;

    [RelayCommand]
    private void HoldSale()
    {
        if (CartItems.Count == 0) return;

        var seq = HeldSales.Select(HoldNumber).DefaultIfEmpty(0).Max() + 1;
        var label = $"#{seq} · {TotalAmount:N0}";
        var items = CartItems.Select(c => new CartItem { VariantId = c.VariantId, PrepackId = c.PrepackId, ProductName = c.ProductName, OriginalPrice = c.OriginalPrice, UnitPrice = c.UnitPrice, Quantity = c.Quantity, Available = c.Available }).ToList();
        HeldSales.Add(new HeldSale(label, items, PaidCash, PaidCard, PaidBonus, SelectedCustomer, DateTime.Now));
        _heldStore.Save(HeldSales);
        ClearCart();
        _toast.Info(L["sale_held"]);
    }

    [RelayCommand]
    private void ResumeSale(HeldSale held)
    {
        ClearCart();
        foreach (var item in held.Items)
            CartItems.Add(item);
        PaidCash = held.PaidCash;
        PaidCard = held.PaidCard;
        PaidBonus = held.PaidBonus;
        SelectedCustomer = held.Customer;
        HeldSales.Remove(held);
        _heldStore.Save(HeldSales);
        IsHeldPanelOpen = false;
        NotifyTotals();
    }

    [RelayCommand]
    private void DiscardHeld(HeldSale held)
    {
        HeldSales.Remove(held);
        _heldStore.Save(HeldSales);
    }

    [RelayCommand]
    private void ToggleHeldPanel() => IsHeldPanelOpen = !IsHeldPanelOpen;

    [RelayCommand]
    private async Task ToggleQueuePanelAsync()
    {
        IsQueuePanelOpen = !IsQueuePanelOpen;
        if (IsQueuePanelOpen) await LoadQueueAsync();
    }

    private async Task LoadQueueAsync()
    {
        if (!CanSeeQueue) return;
        try
        {
            var carts = await _orderingApi.GetAllAsync("Open", kind: "Queue");
            QueueCarts.Clear();
            foreach (var cart in carts) QueueCarts.Add(new QueueRow(cart));
            QueueCount = QueueCarts.Count;
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenQueueCartAsync(QueueRow row)
    {
        IsQueuePanelOpen = false;
        if (await TryLoadCartAsync(row.Cart.AggregateCode))
            await LoadQueueAsync();
    }

    [RelayCommand]
    private async Task CancelQueueCartAsync(QueueRow row)
    {
        try
        {
            await _orderingApi.UpdateStatusAsync(row.Cart.AggregateCode, new Cartex.Shared.Models.Ordering.UpdateCartStatusRequest("Cancelled"));
            await LoadQueueAsync();
        }
        catch { _toast.Warning(L["error"]); }
    }

    private async Task LoadQueueAccessAsync()
    {
        if (!_auth.HasPermission("sales.view")) return;
        try
        {
            var enabled = await _cache.GetAsync(CacheKeys.Features, _featuresApi.GetEnabledAsync);
            CanSeeQueue = enabled.Contains("store") || enabled.Contains("ordering");
        }
        catch { return; }
        if (!CanSeeQueue) return;
        await LoadQueueAsync();
        if (!_queueHubWired)
        {
            _queueHubWired = true;
            _queueHub.CartsChanged += kind => { if (kind == "Queue") _ = LoadQueueAsync(); };
            _queueHub.Resynced += () => _ = LoadQueueAsync();
        }
        _ = _queueHub.EnsureStartedAsync();
    }

    private bool _queueHubWired;
    private string? _saleIdempotencyKey;

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (CartItems.Count == 0) { _toast.Warning(L["no_items"]); return; }

        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) { _toast.Warning(L["select_warehouse"]); return; }
        if (!IsOfflineMode) await RefreshPreviewNowAsync();
        if (PaidBonusBase > 0 && SelectedCustomer is null) { _toast.Warning(L["bonus_customer_required"]); return; }
        if (PaidBonusBase > (SelectedCustomer?.CashbackBalance ?? 0)) { _toast.Warning(L["bonus_insufficient"]); return; }
        if (DebtAmount > 0 && SelectedCustomer is null) { _toast.Warning(L["debt_customer_required"]); return; }
        if (DebtAmount > 0 && !DebtCoveredByCredit && !_allowDebtSales) { _toast.Warning(L["debt_sales_disabled"]); return; }
        if (DebtAmount > 0 && !DebtCoveredByCredit && _requireDebtDueDate && DebtDueDate is null)
        {
            DueDateMissing = true;
            _toast.Warning(L["debt_due_required"]);
            return;
        }

        if (!ServiceLocator.Resolve<ConnectivityService>().IsOnline &&
            !ServiceLocator.Resolve<OfflineSyncService>().IsEnabled)
        {
            _toast.Warning(L["offline_pos_blocked"]);
            return;
        }

        if (IsOfflineMode)
        {
            if (PaidBonus > 0 || CartItems.Any(i => i.IsPrepack) ||
                (IsMulticurrency && PaymentRows.Any(r => r.Amount > 0)))
            {
                _toast.Warning(L["offline_pos_limited"]);
                return;
            }
            if (DebtAmount > 0 && !DebtCoveredByCredit && !await Offline.GetAllowDebtSalesAsync())
            {
                _toast.Warning(L["debt_sales_disabled"]);
                return;
            }
            var draft = new OfflineSaleDraft(warehouseId.Value, SelectedCustomer?.Id, PaidCash, PaidCard,
                CartItems.Select(c => new OfflineSaleItemDraft(c.VariantId, c.Quantity, CanOverridePrice ? c.PriceOverride : null)).ToList(),
                DiscountAmount,
                DebtAmount > 0 && !DebtCoveredByCredit && DebtDueDate is { } offlineDue ? DateOnly.FromDateTime(offlineDue.Date) : null);
            await ServiceLocator.Resolve<OfflineSyncService>().EnqueueSaleAsync(draft);
            ClearCart();
            _toast.Success(L["offline_sale_queued"]);
            await LoadProductsAsync();
            return;
        }

        _saleIdempotencyKey ??= Guid.NewGuid().ToString("N");

        try
        {
            CreateSaleResult result;
            using (_busy.Begin(L["loading"]))
            {
                var items = CartItems.Select(c => new CreateSaleItemRequest(c.VariantId, c.Quantity,
                    c.IsPrepack ? null : CanOverridePrice ? c.PriceOverride : null, c.PrepackId)).ToList();
                var payments = IsMulticurrency
                    ? PaymentRows.Where(r => r.Amount > 0)
                        .Select(r => new SalePaymentRequest(r.Method.Key switch { "card" => "Card", "bonus" => "Bonus", _ => "Cash" }, r.Currency, r.Amount)).ToList()
                    : null;
                var request = new CreateSaleRequest(warehouseId.Value, SelectedCustomer?.Id, PaidCash, PaidCard, PaidBonus, items, DiscountAmount,
                    payments is { Count: > 0 } ? payments : null,
                    IsMulticurrency && SelectedDebtCurrency != _baseCurrency ? SelectedDebtCurrency : null,
                    DebtAmount > 0 && !DebtCoveredByCredit && DebtDueDate is { } dueDate ? DateOnly.FromDateTime(dueDate.Date) : null,
                    IdempotencyKey: _saleIdempotencyKey,
                    CreditAmount: CreditAmount);
                result = await _salesApi.CreateAsync(request);
            }

            if (_activeCartCode is { } cartCode)
            {
                try { await _orderingApi.UpdateStatusAsync(cartCode, new Cartex.Shared.Models.Ordering.UpdateCartStatusRequest("CheckedOut")); }
                catch { }
                _activeCartCode = null;
            }

            ClearCart();
            _toast.Success(L["sale_completed"]);
            await ShowReceiptAsync(result.ReceiptToken);
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    private async Task ShowReceiptAsync(string token)
    {
        try
        {
            CurrentReceipt = await _receiptApi.GetAsync(token);
            IsReceiptOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); return; }

        if (!_printer.AutoPrintEnabled) return;
        try { await PrintCurrentReceiptAsync(); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PrintReceipt()
    {
        if (CurrentReceipt is null) return;
        try
        {
            await PrintCurrentReceiptAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task PrintCurrentReceiptAsync()
    {
        if (CurrentReceipt is null) return;
        var settings = _printer.GetSettings();
        if (settings.ReceiptMode is "a4" or "a5")
        {
            var content = await _receiptApi.GetPdfAsync(CurrentReceipt.ReceiptToken, settings.ReceiptMode);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cartex-chek-{CurrentReceipt.SaleId}.pdf");
            await System.IO.File.WriteAllBytesAsync(path, await content.ReadAsByteArrayAsync());
            _printer.PrintDocument(path, settings.DocumentPrinter);
        }
        else
            _printer.PrintReceipt(CurrentReceipt);
    }

    [RelayCommand]
    private void CloseReceipt() => IsReceiptOpen = false;
}
