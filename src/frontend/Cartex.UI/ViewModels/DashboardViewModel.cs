using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Transactions;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class DashboardViewModel(
    NavigationService navigationService,
    ISalesApi salesApi,
    IProductsApi productsApi,
    IStocksApi stocksApi,
    ITransactionsApi transactionsApi) : ViewModelBase
{
    [ObservableProperty] private string _welcomeMessage = "";
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _todayProfit;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<TransactionDto> RecentTransactions { get; } = [];
    public ObservableCollection<StockDto> LowStockItems { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        WelcomeMessage = $"{L["welcome"]}, {ServiceLocator.Resolve<AuthService>().UserInfo?.FullName ?? ""}!";

        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var sales = await salesApi.GetAllAsync(fromDate: today, toDate: tomorrow);
            TodaySales = sales.Sum(s => s.TotalAmount);
            TodayProfit = TodaySales;
        }
        catch
        {
            TodaySales = 0;
            TodayProfit = 0;
        }

        try
        {
            var products = await productsApi.GetAllAsync();
            TotalProducts = products.Count;
        }
        catch
        {
            TotalProducts = 0;
        }

        try
        {
            var stocks = await stocksApi.GetAllAsync(warehouseId: 1);
            var lowItems = stocks.Where(s => s.Quantity < 10).ToList();
            LowStockCount = lowItems.Count;
            LowStockItems.Clear();
            foreach (var item in lowItems)
                LowStockItems.Add(item);
        }
        catch
        {
            LowStockCount = 0;
            LowStockItems.Clear();
        }

        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var transactions = await transactionsApi.GetAllAsync(fromDate: today, toDate: tomorrow);
            RecentTransactions.Clear();
            foreach (var t in transactions.Take(10))
                RecentTransactions.Add(t);
        }
        catch
        {
            RecentTransactions.Clear();
        }

        IsLoading = false;
    }

    [RelayCommand]
    private void GoToSales() => navigationService.RequestMenuNavigation("pos");

    [RelayCommand]
    private void GoToProducts() => navigationService.RequestMenuNavigation("products");

    [RelayCommand]
    private void GoToCustomers() => navigationService.RequestMenuNavigation("settings");
}
