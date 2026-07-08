using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Units;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public record HeldSale(string Label, List<CartItem> Items, decimal PaidCash, decimal PaidCard, decimal PaidBonus, CustomerDto? Customer, DateTime HeldAt);

public record PayMethodOption(string Key, string Label);

public partial class PaymentRow : ObservableObject
{
    private readonly Action _changed;
    private readonly Func<string, decimal> _rateOf;

    [ObservableProperty] private PayMethodOption? _method;
    [ObservableProperty] private string _currency;
    [ObservableProperty] private decimal _amount;

    public PaymentRow(string currency, PayMethodOption method, Action changed, Func<string, decimal> rateOf)
    {
        _currency = currency;
        _method = method;
        _changed = changed;
        _rateOf = rateOf;
    }

    public decimal AmountBase => Math.Round(Amount * _rateOf(Currency), 2);

    partial void OnMethodChanged(PayMethodOption? value) => _changed();
    partial void OnCurrencyChanged(string value) { OnPropertyChanged(nameof(AmountBase)); _changed(); }
    partial void OnAmountChanged(decimal value) { OnPropertyChanged(nameof(AmountBase)); _changed(); }
}

public partial class CartItem : ObservableObject
{
    public long VariantId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal StockingPrice { get; init; }
    public IReadOnlyList<UnitDto> AvailableUnits { get; init; } = [];
    public UnitDto? StockingUnit { get; init; }
    public decimal OriginalPrice { get; set; }
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity = 1;
    [ObservableProperty] private UnitDto? _selectedUnit;

    public bool HasUnitChoice => AvailableUnits.Count > 1;
    public long? UnitId => SelectedUnit is { } u && u.Id != StockingUnit?.Id ? u.Id : null;
    public decimal? PriceOverride => UnitPrice != OriginalPrice ? UnitPrice : null;
    public decimal LineTotal => UnitPrice * Quantity;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));

    partial void OnSelectedUnitChanged(UnitDto? value)
    {
        if (value is not null && StockingUnit is { Factor: > 0 })
        {
            var price = StockingPrice * value.Factor / StockingUnit.Factor;
            UnitPrice = price;
            OriginalPrice = price;
        }
        OnPropertyChanged(nameof(LineTotal));
    }
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
    private readonly ICustomersApi _customersApi;
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IReceiptApi _receiptApi;
    private readonly IBarcodesApi _barcodesApi;
    private readonly IUnitsApi _unitsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IHeldSaleStore _heldStore;
    private readonly IPrinterService _printer;
    private readonly IScannedCodeParser _scannedCodeParser;
    private readonly AuthService _auth;

    private readonly List<CategoryDto> _allCategories = [];
    private long? _selectedCategoryId;

    public BranchContextService Branch { get; }
    public QuickProductViewModel QuickProduct { get; }

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
    [ObservableProperty] private string? _lastReceiptToken;
    [ObservableProperty] private bool _isReceiptOpen;
    [ObservableProperty] private ReceiptDto? _currentReceipt;
    [ObservableProperty] private bool _isProductDetailOpen;
    [ObservableProperty] private StockOnHandDto? _detailProduct;
    [ObservableProperty] private string _detailBarcodes = string.Empty;

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<StockOnHandDto> Products { get; } = [];
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    public ObservableCollection<HeldSale> HeldSales { get; } = [];
    public ObservableCollection<CategoryChip> NavCategories { get; } = [];

    private readonly List<UnitDto> _allUnits = [];
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
    private string? _activeCartCode;
    private readonly Dictionary<string, decimal> _rates = [];
    private string _baseCurrency = "UZS";

    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private bool _hasStaleRate;
    [ObservableProperty] private string? _selectedDebtCurrency;
    [ObservableProperty] private DateTimeOffset? _debtDueDate;

    public ObservableCollection<PaymentRow> PaymentRows { get; } = [];
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<PayMethodOption> PayMethods { get; } = [];

    private decimal RateOf(string code) => code == _baseCurrency ? 1m : _rates.GetValueOrDefault(code, 0m);
    private decimal PaidBonusBase => IsMulticurrency ? PaymentRows.Where(r => r.Method?.Key == "bonus").Sum(r => r.AmountBase) : PaidBonus;

    public decimal SubTotal => CartItems.Sum(i => i.LineTotal);
    public decimal TotalAmount => Math.Max(0, SubTotal - DiscountAmount);
    public decimal TotalPaid => IsMulticurrency ? PaymentRows.Sum(r => r.AmountBase) : PaidCash + PaidCard + PaidBonus;
    public decimal ChangeAmount => TotalPaid > TotalAmount ? TotalPaid - TotalAmount : 0;
    public decimal DebtAmount => TotalPaid < TotalAmount ? TotalAmount - TotalPaid : 0;
    public bool IsCartEmpty => CartItems.Count == 0;
    public bool HasLastReceipt => !string.IsNullOrEmpty(LastReceiptToken);
    public bool CanOverridePrice => _auth.HasPermission("sales.priceOverride");

    public decimal CustomerDebt => SelectedCustomer?.DebtBalance ?? 0;
    public bool HasCreditLimit => (SelectedCustomer?.CreditLimit ?? 0) > 0;
    public decimal RemainingCredit => Math.Max(0, (SelectedCustomer?.CreditLimit ?? 0) - CustomerDebt);
    public bool IsOverCreditLimit => HasCreditLimit && CustomerDebt + DebtAmount > SelectedCustomer!.CreditLimit;

    public SalesViewModel(ISalesApi salesApi, IStocksApi stocksApi, ICustomersApi customersApi,
        IProductsApi productsApi, ICategoriesApi categoriesApi, IReceiptApi receiptApi, IBarcodesApi barcodesApi, IUnitsApi unitsApi, BranchContextService branch,
        QuickProductViewModel quickProduct, IToastService toast, IBusyService busy, IHeldSaleStore heldStore, IPrinterService printer, IScannedCodeParser scannedCodeParser, AuthService auth,
        IBusinessApi businessApi, IRatesApi ratesApi, IOrderingApi orderingApi, PosHandoffService handoff)
    {
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _orderingApi = orderingApi;
        _handoff = handoff;
        PaymentRows.CollectionChanged += (_, _) => NotifyTotals();
        _salesApi = salesApi;
        _stocksApi = stocksApi;
        _customersApi = customersApi;
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _receiptApi = receiptApi;
        _barcodesApi = barcodesApi;
        _unitsApi = unitsApi;
        Branch = branch;
        QuickProduct = quickProduct;
        QuickProduct.Created += OnQuickProductCreated;
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

    public async Task LoadAsync()
    {
        await LoadMulticurrencyAsync();
        await LoadUnitsAsync();
        await LoadCategoriesAsync();
        await LoadProductsAsync();

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
                AddToCart(item.VariantId, item.ProductName, item.UnitPrice,
                    stock?.Dimension ?? "Count", stock?.UnitName ?? "", item.Quantity);
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

    private async Task LoadMulticurrencyAsync()
    {
        try
        {
            var business = await _businessApi.GetAsync();
            _baseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            if (!IsMulticurrency) return;

            var rates = await _ratesApi.GetCurrentAsync();
            _rates.Clear();
            foreach (var r in rates) _rates[r.Code] = r.Rate;
            HasStaleRate = rates.Any(r => r.EffectiveAt < DateTime.UtcNow.AddDays(-3));

            Currencies.Clear();
            Currencies.Add(_baseCurrency);
            foreach (var r in rates.OrderBy(r => r.Code)) Currencies.Add(r.Code);

            PayMethods.Clear();
            PayMethods.Add(new PayMethodOption("cash", L["cash"]));
            PayMethods.Add(new PayMethodOption("card", L["card"]));
            PayMethods.Add(new PayMethodOption("bonus", L["bonus"]));

            SelectedDebtCurrency = _baseCurrency;
            if (PaymentRows.Count == 0)
                AddPayment();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void AddPayment()
    {
        var row = new PaymentRow(_baseCurrency, PayMethods.FirstOrDefault() ?? new PayMethodOption("cash", L["cash"]), NotifyTotals, RateOf);
        row.PropertyChanged += (_, _) => NotifyTotals();
        PaymentRows.Add(row);
    }

    [RelayCommand]
    private void RemovePayment(PaymentRow row) => PaymentRows.Remove(row);

    private async Task LoadUnitsAsync()
    {
        if (_allUnits.Count > 0) return;
        try
        {
            var units = await _unitsApi.GetAllAsync();
            _allUnits.AddRange(units);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _categoriesApi.GetAllAsync();
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
    }

    [RelayCommand]
    private async Task SelectCategory(CategoryChip chip)
    {
        _selectedCategoryId = chip.Id;
        foreach (var c in NavCategories)
            c.IsSelected = c.Id == _selectedCategoryId;
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
    }

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(CustomerDebt));
        OnPropertyChanged(nameof(HasCreditLimit));
        OnPropertyChanged(nameof(RemainingCredit));
        OnPropertyChanged(nameof(IsOverCreditLimit));
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
    partial void OnLastReceiptTokenChanged(string? value) => OnPropertyChanged(nameof(HasLastReceipt));

    private Task LoadProductsAsync() => LoadProductsPageAsync(reset: true);

    private async Task LoadProductsPageAsync(bool reset)
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;

        try
        {
            _productsPage = reset ? 1 : _productsPage + 1;
            var search = SearchText.Trim();
            var page = await _stocksApi.GetOnHandAsync(warehouseId.Value, _selectedCategoryId,
                string.IsNullOrEmpty(search) ? null : search, _productsPage, PosPageSize);
            if (reset) Products.Clear();
            foreach (var s in page.Items) Products.Add(s);
            ProductsTotal = page.TotalCount;
            OnPropertyChanged(nameof(HasMoreProducts));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task LoadMoreProducts() => LoadProductsPageAsync(reset: false);

    [RelayCommand]
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

        if (code.Length == 32 && code.All(char.IsAsciiHexDigit))
        {
            if (await TryLoadCartAsync(code))
            {
                SearchText = "";
                return;
            }
        }

        var scanned = _scannedCodeParser.Parse(code);

        try
        {
            var product = await _productsApi.GetByBarcodeAsync(scanned.Code, warehouseId.Value);
            var quantity = scanned.Type == ScannedCodeType.Weighted && scanned.Weight is { } weight
                ? weight
                : product.PackQty > 1 ? product.PackQty : 1;
            AddToCart(product.VariantId, product.ProductName, product.SellingPrice, product.Dimension, product.UnitName, quantity);
            SearchText = string.Empty;
            return;
        }
        catch (ApiException)
        {
        }

        try
        {
            var customer = await _customersApi.GetByCardAsync(code);
            if (customer is not null)
            {
                SelectedCustomer = customer;
                SearchText = string.Empty;
                _toast.Success(customer.FullName);
                return;
            }
        }
        catch (ApiException)
        {
        }

        if (Products.Count > 0)
        {
            var first = Products[0];
            AddToCart(first.VariantId, first.ProductName, first.SellingPrice, first.Dimension, first.UnitName);
            SearchText = string.Empty;
            return;
        }

        SearchText = string.Empty;
        await QuickProduct.OpenCommand.ExecuteAsync(null);
        QuickProduct.Barcode = code;
    }

    private void OnQuickProductCreated(long variantId, string name)
    {
        var unit = QuickProduct.SelectedUnit;
        AddToCart(variantId, name, QuickProduct.SellingPrice ?? 0, unit?.Dimension ?? "Count", unit?.Name ?? string.Empty);
    }

    [RelayCommand]
    private void AddStockToCart(StockOnHandDto stock) =>
        AddToCart(stock.VariantId, stock.ProductName, stock.SellingPrice, stock.Dimension, stock.UnitName);

    public void ShowProductDetail(StockOnHandDto product)
    {
        DetailProduct = product;
        DetailBarcodes = string.Empty;
        IsProductDetailOpen = true;
        _ = LoadDetailBarcodesAsync(product.VariantId);
    }

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

    private void AddToCart(long variantId, string name, decimal price, string dimension, string unitName, decimal quantity = 1)
    {
        var existing = CartItems.FirstOrDefault(c => c.VariantId == variantId);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            NotifyTotals();
            return;
        }

        List<UnitDto> units = dimension == "Count" ? [] : _allUnits.Where(u => u.Dimension == dimension).ToList();
        var stocking = units.FirstOrDefault(u => u.Name == unitName);
        CartItems.Add(new CartItem
        {
            VariantId = variantId,
            ProductName = name,
            StockingPrice = price,
            OriginalPrice = price,
            UnitPrice = price,
            Quantity = quantity,
            AvailableUnits = units,
            StockingUnit = stocking,
            SelectedUnit = stocking
        });
        NotifyTotals();
    }

    [RelayCommand]
    private void Increment(CartItem item) { item.Quantity += 1; NotifyTotals(); }

    [RelayCommand]
    private void Decrement(CartItem item)
    {
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
            PaymentRows.Clear();
            AddPayment();
            PaymentRows[0].Amount = TotalAmount;
            return;
        }
        PaidCash = TotalAmount; PaidCard = 0; PaidBonus = 0;
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        _activeCartCode = null;
        DebtDueDate = null;
        PaidCash = PaidCard = PaidBonus = DiscountAmount = 0;
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
            var customers = await _customersApi.GetAllAsync(string.IsNullOrEmpty(query) ? null : query);
            CustomerResults.Clear();
            foreach (var c in customers.Take(30))
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
        var items = CartItems.Select(c => new CartItem { VariantId = c.VariantId, ProductName = c.ProductName, StockingPrice = c.StockingPrice, AvailableUnits = c.AvailableUnits, StockingUnit = c.StockingUnit, SelectedUnit = c.SelectedUnit, OriginalPrice = c.OriginalPrice, UnitPrice = c.UnitPrice, Quantity = c.Quantity }).ToList();
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
    private void OpenReceipt()
    {
        if (string.IsNullOrEmpty(LastReceiptToken)) return;
        var url = $"{SettingsService.Instance.ApiBaseUrl}/r/{LastReceiptToken}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (CartItems.Count == 0) return;

        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) { _toast.Warning(L["select_warehouse"]); return; }
        if (PaidBonusBase > 0 && SelectedCustomer is null) { _toast.Warning(L["customer"]); return; }
        if (PaidBonusBase > (SelectedCustomer?.CashbackBalance ?? 0)) { _toast.Warning(L["cashback_balance"]); return; }
        if (DebtAmount > 0 && SelectedCustomer is null) { _toast.Warning(L["customer"]); return; }

        try
        {
            CreateSaleResult result;
            using (_busy.Begin(L["loading"]))
            {
                var items = CartItems.Select(c => new CreateSaleItemRequest(c.VariantId, c.Quantity, CanOverridePrice ? c.PriceOverride : null, c.UnitId)).ToList();
                var payments = IsMulticurrency
                    ? PaymentRows.Where(r => r.Amount > 0 && r.Method is not null)
                        .Select(r => new SalePaymentRequest(r.Method!.Key switch { "card" => "Card", "bonus" => "Bonus", _ => "Cash" }, r.Currency, r.Amount)).ToList()
                    : null;
                var request = new CreateSaleRequest(warehouseId.Value, SelectedCustomer?.Id, PaidCash, PaidCard, PaidBonus, items, DiscountAmount,
                    payments is { Count: > 0 } ? payments : null,
                    IsMulticurrency && SelectedDebtCurrency != _baseCurrency ? SelectedDebtCurrency : null,
                    DebtAmount > 0 && DebtDueDate is { } dueDate ? DateOnly.FromDateTime(dueDate.Date) : null);
                result = await _salesApi.CreateAsync(request);
            }

            if (_activeCartCode is { } cartCode)
            {
                try { await _orderingApi.UpdateStatusAsync(cartCode, new Cartex.Shared.Models.Ordering.UpdateCartStatusRequest("CheckedOut")); }
                catch { }
                _activeCartCode = null;
            }

            LastReceiptToken = result.ReceiptToken;
            ClearCart();
            _toast.Success(L["complete_sale"]);
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
            if (_printer.AutoPrintEnabled)
                _printer.PrintReceipt(CurrentReceipt);
        }
        catch { }
    }

    [RelayCommand]
    private void PrintReceipt()
    {
        if (CurrentReceipt is null) return;
        try { _printer.PrintReceipt(CurrentReceipt); _toast.Success(L["success"]); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseReceipt() => IsReceiptOpen = false;
}
