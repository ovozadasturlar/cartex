using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Customers;
using Cartex.UI.Services;
using Refit;

namespace Cartex.UI.ViewModels;

public record HeldSale(string Label, List<CartItem> Items, decimal PaidCash, decimal PaidCard, decimal PaidBonus, CustomerDto? Customer, DateTime HeldAt);

public partial class CategoryItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

public partial class CartItem : ObservableObject
{
    public long ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity = 1;

    public decimal LineTotal => UnitPrice * Quantity;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public partial class SalesViewModel : ViewModelBase, ILoadable
{
    private readonly ISalesApi _salesApi;
    private readonly IStocksApi _stocksApi;
    private readonly ICustomersApi _customersApi;
    private readonly IProductsApi _productsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    public BranchContextService Branch { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _customerSearch = string.Empty;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private decimal _paidCash;
    [ObservableProperty] private decimal _paidCard;
    [ObservableProperty] private decimal _paidBonus;
    [ObservableProperty] private decimal _discountAmount;
    [ObservableProperty] private bool _isCustomerPanelOpen;
    [ObservableProperty] private bool _isHeldPanelOpen;
    [ObservableProperty] private string? _lastReceiptToken;

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<StockOnHandDto> Products { get; } = [];
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    public ObservableCollection<HeldSale> HeldSales { get; } = [];
    public ObservableCollection<CategoryItem> Categories { get; } = [];

    private readonly List<StockOnHandDto> _allProducts = [];

    public decimal SubTotal => CartItems.Sum(i => i.LineTotal);
    public decimal TotalAmount => Math.Max(0, SubTotal - DiscountAmount);
    public decimal TotalPaid => PaidCash + PaidCard + PaidBonus;
    public decimal ChangeAmount => TotalPaid > TotalAmount ? TotalPaid - TotalAmount : 0;
    public decimal DebtAmount => TotalPaid < TotalAmount ? TotalAmount - TotalPaid : 0;
    public bool IsCartEmpty => CartItems.Count == 0;
    public bool HasLastReceipt => !string.IsNullOrEmpty(LastReceiptToken);

    public SalesViewModel(ISalesApi salesApi, IStocksApi stocksApi, ICustomersApi customersApi,
        IProductsApi productsApi, BranchContextService branch, IToastService toast, IBusyService busy)
    {
        _salesApi = salesApi;
        _stocksApi = stocksApi;
        _customersApi = customersApi;
        _productsApi = productsApi;
        Branch = branch;
        _toast = toast;
        _busy = busy;

        CartItems.CollectionChanged += (_, _) => NotifyTotals();
        Branch.PropertyChanged += OnBranchChanged;
    }

    public Task LoadAsync() => LoadProductsAsync();

    private void OnBranchChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchContextService.SelectedWarehouse))
            _ = LoadProductsAsync();
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalPaid));
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(DebtAmount));
        OnPropertyChanged(nameof(IsCartEmpty));
    }

    partial void OnPaidCashChanged(decimal value) => NotifyTotals();
    partial void OnPaidCardChanged(decimal value) => NotifyTotals();
    partial void OnPaidBonusChanged(decimal value) => NotifyTotals();
    partial void OnDiscountAmountChanged(decimal value) => NotifyTotals();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnLastReceiptTokenChanged(string? value) => OnPropertyChanged(nameof(HasLastReceipt));

    private async Task LoadProductsAsync()
    {
        var warehouseId = Branch.CurrentWarehouseId;
        if (warehouseId is null) return;

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var stocks = await _stocksApi.GetOnHandAsync(warehouseId.Value);
                _allProducts.Clear();
                _allProducts.AddRange(stocks);

                Categories.Clear();
                Categories.Add(new CategoryItem { Name = L["all"], IsSelected = true });
                foreach (var c in stocks.Where(s => !string.IsNullOrEmpty(s.CategoryName))
                             .Select(s => s.CategoryName!).Distinct().OrderBy(c => c))
                    Categories.Add(new CategoryItem { Name = c });

                ApplyFilter();
            }
        }
        catch
        {
            _toast.Error(L["error"]);
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var category = Categories.FirstOrDefault(c => c.IsSelected)?.Name;
        IEnumerable<StockOnHandDto> source = _allProducts;

        if (!string.IsNullOrEmpty(category) && category != L["all"])
            source = source.Where(s => string.Equals(s.CategoryName, category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(query))
            source = source.Where(s => s.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase));

        Products.Clear();
        foreach (var s in source)
            Products.Add(s);
    }

    [RelayCommand]
    private void SelectCategory(CategoryItem category)
    {
        foreach (var c in Categories)
            c.IsSelected = c == category;
        ApplyFilter();
    }

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

        try
        {
            var product = await _productsApi.GetByBarcodeAsync(code, warehouseId.Value);
            AddToCart(product.ProductId, product.ProductName, product.SellingPrice);
            SearchText = string.Empty;
            return;
        }
        catch (ApiException)
        {
        }

        if (Products.Count > 0)
        {
            var first = Products[0];
            AddToCart(first.ProductId, first.ProductName, first.SellingPrice);
            SearchText = string.Empty;
        }
        else
        {
            _toast.Warning($"{L["error"]}: {code}");
        }
    }

    [RelayCommand]
    private void AddStockToCart(StockOnHandDto stock) =>
        AddToCart(stock.ProductId, stock.ProductName, stock.SellingPrice);

    private void AddToCart(long productId, string name, decimal price)
    {
        var existing = CartItems.FirstOrDefault(c => c.ProductId == productId);
        if (existing is not null)
            existing.Quantity += 1;
        else
            CartItems.Add(new CartItem { ProductId = productId, ProductName = name, UnitPrice = price });
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
    private void PayExact() { PaidCash = TotalAmount; PaidCard = 0; PaidBonus = 0; }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        PaidCash = PaidCard = PaidBonus = DiscountAmount = 0;
        SelectedCustomer = null;
        NotifyTotals();
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
        catch
        {
            _toast.Error(L["error"]);
        }
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
        if (IsCustomerPanelOpen && CustomerResults.Count == 0)
            _ = SearchCustomerAsync();
    }

    [RelayCommand]
    private void HoldSale()
    {
        if (CartItems.Count == 0) return;

        var label = $"#{HeldSales.Count + 1} · {TotalAmount:N0}";
        var items = CartItems.Select(c => new CartItem { ProductId = c.ProductId, ProductName = c.ProductName, UnitPrice = c.UnitPrice, Quantity = c.Quantity }).ToList();
        HeldSales.Add(new HeldSale(label, items, PaidCash, PaidCard, PaidBonus, SelectedCustomer, DateTime.Now));
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
        IsHeldPanelOpen = false;
        NotifyTotals();
    }

    [RelayCommand]
    private void DiscardHeld(HeldSale held) => HeldSales.Remove(held);

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
        if (PaidBonus > 0 && SelectedCustomer is null) { _toast.Warning(L["customer"]); return; }
        if (PaidBonus > (SelectedCustomer?.CashbackBalance ?? 0)) { _toast.Warning(L["cashback_balance"]); return; }
        if (DebtAmount > 0 && SelectedCustomer is null) { _toast.Warning(L["customer"]); return; }

        try
        {
            long saleId;
            using (_busy.Begin(L["loading"]))
            {
                var items = CartItems.Select(c => new CreateSaleItemRequest(c.ProductId, c.Quantity)).ToList();
                var request = new CreateSaleRequest(warehouseId.Value, SelectedCustomer?.Id, PaidCash, PaidCard, PaidBonus, items);
                saleId = await _salesApi.CreateAsync(request);
            }

            var change = ChangeAmount;
            await ResolveReceiptAsync(saleId, warehouseId.Value);
            ClearCart();
            _toast.Success(change > 0 ? $"{L["complete_sale"]} · {L["change"]}: {change:N0}" : L["complete_sale"]);
            await LoadProductsAsync();
        }
        catch (Exception ex)
        {
            _toast.Error(ex is ApiException ? L["error"] : ex.Message);
        }
    }

    private async Task ResolveReceiptAsync(long saleId, long warehouseId)
    {
        try
        {
            var sales = await _salesApi.GetAllAsync(warehouseId, DateTime.Today, DateTime.Today.AddDays(1));
            LastReceiptToken = sales.FirstOrDefault(s => s.Id == saleId)?.ReceiptToken;
        }
        catch
        {
            LastReceiptToken = null;
        }
    }
}
