using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Reports;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Transactions;
using Cartex.UI.Controls;
using Cartex.UI.Services;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cartex.UI.ViewModels;

public partial class DashboardViewModel(
    NavigationService navigationService,
    IReportsApi reportsApi,
    IProductsApi productsApi,
    IStocksApi stocksApi,
    ITransactionsApi transactionsApi,
    BranchContextService branch,
    IToastService toast) : ViewModelBase, ILoadable
{
    private const double ChartHeight = 150;
    private static readonly string[] PieColors = ["#166534", "#059669", "#0EA5E9", "#F59E0B", "#DC2626", "#8B5CF6"];

    [ObservableProperty] private string _welcomeMessage = "";
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _todayProfit;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<TransactionDto> RecentTransactions { get; } = [];
    public ObservableCollection<LowStockDto> LowStockItems { get; } = [];
    public ObservableCollection<CustomerSalesDto> TopCustomers { get; } = [];
    public ObservableCollection<TopProductReportDto> TopProducts { get; } = [];

    public ObservableCollection<ISeries> CashFlowSeries { get; } = [];
    [ObservableProperty] private Axis[] _cashFlowXAxes = [new Axis()];
    [ObservableProperty] private Axis[] _cashFlowYAxes = [new Axis { MinLimit = 0 }];
    public ObservableCollection<ChartColumn> CategoryBars { get; } = [];

    private static LineSeries<decimal> FogLine(decimal[] values, string name, string hex) =>
        new()
        {
            Values = values,
            Name = name,
            Stroke = new SolidColorPaint(SKColor.Parse(hex), 2.5f),
            Fill = new LinearGradientPaint(
                [SKColor.Parse(hex).WithAlpha(70), SKColor.Parse(hex).WithAlpha(0)],
                new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
            GeometrySize = 0,
            GeometryStroke = null,
            GeometryFill = null,
            LineSmoothness = 0.35
        };

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        WelcomeMessage = $"{L["welcome"]}, {ServiceLocator.Resolve<AuthService>().UserInfo?.FullName ?? ""}!";

        var todayStart = new DateTimeOffset(DateTime.Today).UtcDateTime;
        var todayEnd = new DateTimeOffset(DateTime.Today.AddDays(1)).UtcDateTime;
        var weekStart = new DateTimeOffset(DateTime.Today.AddDays(-6)).UtcDateTime;
        var warehouseId = branch.CurrentWarehouseId;

        var todayReportTask = reportsApi.GetSalesReportAsync(todayStart, todayEnd);
        var totalsTask = productsApi.GetTotalsAsync();
        var categoriesTask = productsApi.GetCategoryCountsAsync();
        var lowStockTask = warehouseId is { } wid ? stocksApi.GetLowStockAsync(wid) : null;
        var transactionsTask = transactionsApi.GetPagedAsync(1, 10, sortBy: "CreatedAt", descending: true,
            fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(1));
        var weekReportTask = reportsApi.GetSalesReportAsync(weekStart, todayEnd);
        var flowTask = reportsApi.GetCashFlowAsync(weekStart, todayEnd);
        var topCustomersTask = reportsApi.GetTopCustomersAsync(weekStart, todayEnd);

        try
        {
            var report = await todayReportTask;
            TodaySales = report.Revenue;
            TodayProfit = report.Profit;
        }
        catch (Exception ex)
        {
            TodaySales = 0;
            TodayProfit = 0;
            toast.Error(ApiErrors.Describe(ex));
        }

        try
        {
            TotalProducts = (await totalsTask).Count;
            var categoryGroups = (await categoriesTask).Take(PieColors.Length).ToList();

            CategoryBars.Clear();
            var maxCount = categoryGroups.Count > 0 ? categoryGroups.Max(g => g.Count) : 0;
            for (var i = 0; i < categoryGroups.Count; i++)
            {
                var brush = new SolidColorBrush(Color.Parse(PieColors[i % PieColors.Length]));
                var height = maxCount > 0 ? categoryGroups[i].Count / (double)maxCount * ChartHeight : 0;
                CategoryBars.Add(new ChartColumn(categoryGroups[i].Name ?? "—", categoryGroups[i].Count.ToString(),
                    [new ChartBar(height, brush)]));
            }
        }
        catch
        {
            TotalProducts = 0;
            CategoryBars.Clear();
        }

        try
        {
            LowStockItems.Clear();
            if (lowStockTask is not null)
            {
                var lowItems = await lowStockTask;
                LowStockCount = lowItems.Count;
                foreach (var item in lowItems)
                    LowStockItems.Add(item);
            }
            else
                LowStockCount = 0;
        }
        catch (Exception ex)
        {
            LowStockCount = 0;
            LowStockItems.Clear();
            toast.Error(ApiErrors.Describe(ex));
        }

        try
        {
            var transactions = await transactionsTask;
            RecentTransactions.Clear();
            foreach (var t in transactions.Content ?? [])
                RecentTransactions.Add(t);
        }
        catch
        {
            RecentTransactions.Clear();
        }

        try
        {
            var report = await weekReportTask;
            var flow = await flowTask;

            CashFlowSeries.Clear();
            CashFlowSeries.Add(FogLine([.. flow.Select(f => f.Sales)], L["sales"], "#2563EB"));
            CashFlowSeries.Add(FogLine([.. flow.Select(f => f.Income)], L["income"], "#166534"));
            CashFlowSeries.Add(FogLine([.. flow.Select(f => f.Expense)], L["expense"], "#DC2626"));
            CashFlowXAxes = [new Axis
            {
                Labels = [.. flow.Select(f => f.Date.ToString("dd/MM"))],
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                TextSize = 12
            }];
            CashFlowYAxes = [new Axis { MinLimit = 0, LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")), TextSize = 12 }];

            TopProducts.Clear();
            foreach (var p in report.TopProducts.Take(2)) TopProducts.Add(p);
        }
        catch
        {
            CashFlowSeries.Clear();
            TopProducts.Clear();
        }

        try
        {
            var top = await topCustomersTask;
            TopCustomers.Clear();
            foreach (var c in top.Take(2)) TopCustomers.Add(c);
        }
        catch
        {
            TopCustomers.Clear();
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
