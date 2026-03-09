using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Customers;
using Cartex.Desktop.Services;

namespace Cartex.Desktop.ViewModels;

public partial class CartItem : ObservableObject
{
    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private long _productId;

    [ObservableProperty]
    private long _stockId;

    [ObservableProperty]
    private decimal _unitPrice;

    [ObservableProperty]
    private decimal _quantity = 1;

    public decimal LineTotal => UnitPrice * Quantity;

    partial void OnQuantityChanged(decimal value) =>
        OnPropertyChanged(nameof(LineTotal));

    partial void OnUnitPriceChanged(decimal value) =>
        OnPropertyChanged(nameof(LineTotal));
}

public partial class SalesViewModel : ViewModelBase
{
    private readonly ISalesApi _salesApi;
    private readonly IStocksApi _stocksApi;
    private readonly ICustomersApi _customersApi;
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _barcodeInput = string.Empty;

    [ObservableProperty]
    private decimal _paidCash;

    [ObservableProperty]
    private decimal _paidCard;

    [ObservableProperty]
    private decimal _paidBonus;

    [ObservableProperty]
    private CustomerDto? _selectedCustomer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private long _selectedWarehouseId = 1;

    public ObservableCollection<CartItem> CartItems { get; } = [];
    public ObservableCollection<StockDto> AvailableStocks { get; } = [];
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

    [RelayCommand]
    private async Task LoadStocksAsync()
    {
        try
        {
            var stocks = await _stocksApi.GetAllAsync(SelectedWarehouseId);
            AvailableStocks.Clear();
            foreach (var s in stocks)
                AvailableStocks.Add(s);
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

        var existing = CartItems.FirstOrDefault(c => c.StockId == stock.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
        }
        else
        {
            CartItems.Add(new CartItem
            {
                ProductName = stock.ProductName,
                ProductId = stock.Id,
                StockId = stock.Id,
                UnitPrice = stock.SellingPrice,
                Quantity = 1
            });
        }

        BarcodeInput = string.Empty;
        NotifyTotals();
    }

    [RelayCommand]
    private void AddStockToCart(StockDto stock)
    {
        var existing = CartItems.FirstOrDefault(c => c.StockId == stock.Id);
        if (existing is not null)
        {
            existing.Quantity += 1;
        }
        else
        {
            CartItems.Add(new CartItem
            {
                ProductName = stock.ProductName,
                ProductId = stock.Id,
                StockId = stock.Id,
                UnitPrice = stock.SellingPrice,
                Quantity = 1
            });
        }
        NotifyTotals();
    }

    [RelayCommand]
    private void RemoveCartItem(CartItem item)
    {
        CartItems.Remove(item);
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

            CartItems.Clear();
            PaidCash = 0;
            PaidCard = 0;
            PaidBonus = 0;
            SelectedCustomer = null;
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
