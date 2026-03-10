using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Transactions;
using Cartex.UI.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cartex.UI.ViewModels;

public partial class DashboardViewModel(
    NavigationService navigationService,
    ISalesApi salesApi,
    IProductsApi productsApi,
    IStocksApi stocksApi,
    ITransactionsApi transactionsApi) : ViewModelBase
{
    private static readonly string[] PieColors = ["#166534", "#059669", "#0EA5E9", "#F59E0B", "#DC2626", "#8B5CF6"];

    [ObservableProperty] private string _welcomeMessage = "";
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _todayProfit;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<TransactionDto> RecentTransactions { get; } = [];
    public ObservableCollection<StockDto> LowStockItems { get; } = [];

    public ObservableCollection<ISeries> RevenueSeries { get; } = [];
    public ObservableCollection<ISeries> CategorySeries { get; } = [];

    [ObservableProperty] private Axis[] _revenueXAxes = [new Axis()];
    [ObservableProperty] private Axis[] _revenueYAxes = [new Axis()];

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

            var categoryGroups = products
                .GroupBy(p => p.CategoryName ?? "—")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(PieColors.Length)
                .ToList();

            CategorySeries.Clear();
            for (var i = 0; i < categoryGroups.Count; i++)
            {
                var color = SKColor.Parse(PieColors[i % PieColors.Length]);
                CategorySeries.Add(new PieSeries<int>
                {
                    Values = [categoryGroups[i].Count],
                    Name = categoryGroups[i].Name,
                    Fill = new SolidColorPaint(color),
                    InnerRadius = 40
                });
            }
        }
        catch
        {
            TotalProducts = 0;
            CategorySeries.Clear();
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

        try
        {
            var weekAgo = DateTime.Today.AddDays(-6);
            var weekSales = await salesApi.GetAllAsync(fromDate: weekAgo, toDate: DateTime.Today.AddDays(1));
            var dailyTotals = Enumerable.Range(0, 7)
                .Select(i => weekAgo.AddDays(i))
                .Select(d => weekSales.Where(s => s.SaleDate.Date == d).Sum(s => s.TotalAmount))
                .ToArray();

            RevenueSeries.Clear();
            RevenueSeries.Add(new LineSeries<decimal>
            {
                Values = dailyTotals,
                Fill = new SolidColorPaint(SKColor.Parse("#166534").WithAlpha(40)),
                Stroke = new SolidColorPaint(SKColor.Parse("#166534"), 2),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#166534")),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#FFFFFF"), 2),
                GeometrySize = 8,
                LineSmoothness = 0.3
            });

            RevenueXAxes = [new Axis
            {
                Labels = Enumerable.Range(0, 7).Select(i => weekAgo.AddDays(i).ToString("dd/MM")).ToArray(),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                TextSize = 12
            }];

            RevenueYAxes = [new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                TextSize = 12
            }];
        }
        catch
        {
            RevenueSeries.Clear();
            RevenueXAxes = [new Axis()];
            RevenueYAxes = [new Axis()];
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
