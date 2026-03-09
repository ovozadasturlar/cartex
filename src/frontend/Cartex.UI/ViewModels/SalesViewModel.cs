using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Customers;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class CartItem : ObservableObject
{
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private long _productId;
    [ObservableProperty] private long _stockId;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _quantity = 1;

    public decimal LineTotal => UnitPrice * Quantity;

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}

public partial class SalesViewModel : ViewModelBase
{
    private readonly ISalesApi _salesApi;
    private readonly IStocksApi _stocksApi;
    private readonly ICustomersApi _customersApi;
    private readonly AuthService _authService;

    [ObservableProperty] private string _barcodeInput = string.Empty;
    [ObservableProperty] private decimal _paidCash;
    [ObservableProperty] private decimal _paidCard;
    [ObservableProperty] private decimal _paidBonus;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private long _selectedWarehouseId = 1;
    [ObservableProperty] private bool _isTouchMode;
    [ObservableProperty] private string _numpadDisplay = "0";
    [ObservableProperty] private string _numpadTarget = "cash";
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private CartItem? _selectedCartItem;

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<StockDto> AvailableStocks { get; } = [];
    public ObservableCollection<StockDto> FilteredStocks { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];

    public decimal TotalAmount => CartItems.Sum(i => i.LineTotal);
    public decimal TotalPaid => PaidCash + PaidCard + PaidBonus;
    public decimal ChangeAmount => TotalPaid > TotalAmount ? TotalPaid - TotalAmount : 0;
    public decimal DebtAmount => TotalPaid < TotalAmount ? TotalAmount - TotalPaid : 0;

    public SalesViewModel(ISalesApi salesApi, IStocksApi stocksApi, ICustomersApi customersApi, AuthService authService)
    {
        _salesApi = salesApi;
        _stocksApi = stocksApi;
        _customersApi = customersApi;
        _authService = authService;

        CartItems.CollectionChanged += (_, _) => NotifyTotals();
        IsTouchMode = TouchModeManager.Instance.IsTouchMode;
        TouchModeManager.Instance.PropertyChanged += OnTouchModeChanged;
    }

    private void OnTouchModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TouchModeManager.IsTouchMode))
            IsTouchMode = TouchModeManager.Instance.IsTouchMode;
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalPaid));
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(DebtAmount));
    }

    partial void OnPaidCashChanged(decimal value) => NotifyTotals();
    partial void OnPaidCardChanged(decimal value) => NotifyTotals();
    partial void OnPaidBonusChanged(decimal value) => NotifyTotals();

    partial void OnSearchQueryChanged(string value)
    {
        FilteredStocks.Clear();
        var query = value?.Trim() ?? "";
        var source = string.IsNullOrEmpty(query)
            ? AvailableStocks
            : AvailableStocks.Where(s => s.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var s in source)
            FilteredStocks.Add(s);
    }

    [RelayCommand]
    private async Task LoadStocksAsync()
    {
        try
        {
            var stocks = await _stocksApi.GetAllAsync(SelectedWarehouseId);
            AvailableStocks.Clear();
            FilteredStocks.Clear();
            foreach (var s in stocks)
            {
                AvailableStocks.Add(s);
                FilteredStocks.Add(s);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customersApi.GetAllAsync();
            Customers.Clear();
            foreach (var c in customers)
                Customers.Add(c);
        }
        catch { }
    }

    [RelayCommand]
    private void AddByBarcode()
    {
        if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

        var stock = AvailableStocks.FirstOrDefault(s =>
            s.ProductName.Contains(BarcodeInput, StringComparison.OrdinalIgnoreCase));

        if (stock is null)
        {
            StatusMessage = $"Product not found: {BarcodeInput}";
            BarcodeInput = string.Empty;
            return;
        }

        AddStockToCart(stock);
        BarcodeInput = string.Empty;
    }

    [RelayCommand]
    private void AddStockToCart(StockDto stock)
    {
        var existing = CartItems.FirstOrDefault(c => c.StockId == stock.Id);
        if (existing is not null)
            existing.Quantity += 1;
        else
            CartItems.Add(new CartItem
            {
                ProductName = stock.ProductName,
                ProductId = stock.Id,
                StockId = stock.Id,
                UnitPrice = stock.SellingPrice,
                Quantity = 1
            });
        NotifyTotals();
    }

    [RelayCommand]
    private void RemoveCartItem(CartItem item)
    {
        CartItems.Remove(item);
        NotifyTotals();
    }

    [RelayCommand]
    private void IncrementCartItem(CartItem item)
    {
        item.Quantity += 1;
        NotifyTotals();
    }

    [RelayCommand]
    private void DecrementCartItem(CartItem item)
    {
        if (item.Quantity <= 1)
            CartItems.Remove(item);
        else
            item.Quantity -= 1;
        NotifyTotals();
    }

    [RelayCommand]
    private void NumpadPress(string key)
    {
        switch (key)
        {
            case "C":
                NumpadDisplay = "0";
                break;
            case "⌫":
                NumpadDisplay = NumpadDisplay.Length > 1 ? NumpadDisplay[..^1] : "0";
                break;
            case ".":
                if (!NumpadDisplay.Contains('.'))
                    NumpadDisplay += ".";
                break;
            default:
                NumpadDisplay = NumpadDisplay == "0" ? key : NumpadDisplay + key;
                break;
        }
    }

    [RelayCommand]
    private void NumpadApply()
    {
        if (!decimal.TryParse(NumpadDisplay, out var value)) return;

        switch (NumpadTarget)
        {
            case "cash": PaidCash = value; break;
            case "card": PaidCard = value; break;
            case "bonus": PaidBonus = value; break;
            case "qty" when SelectedCartItem is not null:
                SelectedCartItem.Quantity = value;
                NotifyTotals();
                break;
        }
        NumpadDisplay = "0";
    }

    [RelayCommand]
    private void SetNumpadTarget(string target)
    {
        NumpadTarget = target;
        NumpadDisplay = "0";
    }

    [RelayCommand]
    private void PayExact()
    {
        PaidCash = TotalAmount;
        PaidCard = 0;
        PaidBonus = 0;
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        PaidCash = 0;
        PaidCard = 0;
        PaidBonus = 0;
        SelectedCustomer = null;
        StatusMessage = null;
        NumpadDisplay = "0";
        NotifyTotals();
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (CartItems.Count == 0) return;

        IsLoading = true;
        StatusMessage = null;

        try
        {
            var items = CartItems.Select(c => new CreateSaleItemRequest(
                c.ProductId, c.StockId, c.Quantity, c.UnitPrice)).ToList();

            var request = new CreateSaleRequest(
                SelectedWarehouseId,
                _authService.UserInfo?.UserId ?? 0,
                SelectedCustomer?.Id,
                PaidCash, PaidCard, PaidBonus,
                items);

            await _salesApi.CreateAsync(request);
            ClearCart();
            StatusMessage = L["success"];
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
