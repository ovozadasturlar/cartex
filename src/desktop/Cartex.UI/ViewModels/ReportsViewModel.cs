using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Reports;
using Cartex.UI.Services;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Cartex.UI.ViewModels;

public partial class ReportsViewModel : ViewModelBase, ILoadable
{
    private readonly IReportsApi _api;
    private readonly ICustomersApi _customers;
    private readonly IExportService _export;
    private readonly IToastService _toast;
    private readonly AuthService _auth;

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _selectedTabIndex;

    private readonly bool[] _loaded = new bool[5];

    [ObservableProperty] private decimal _totalSales;
    [ObservableProperty] private decimal _totalProfit;
    [ObservableProperty] private int _totalTransactions;
    [ObservableProperty] private decimal _averageSale;
    [ObservableProperty] private decimal _maxSale;

    [ObservableProperty] private decimal _debtTotal;
    [ObservableProperty] private decimal _debt0_30;
    [ObservableProperty] private decimal _debt31_60;
    [ObservableProperty] private decimal _debt60Plus;

    [ObservableProperty] private decimal _inventoryTotalCost;
    [ObservableProperty] private decimal _inventoryTotalRetail;

    [ObservableProperty] private decimal _payCash;
    [ObservableProperty] private decimal _payCard;
    [ObservableProperty] private decimal _payBonus;
    [ObservableProperty] private decimal _payDebt;

    public ObservableCollection<TopProductReportDto> TopProducts { get; } = [];
    public ObservableCollection<DebtAgingRowDto> DebtRows { get; } = [];
    public ObservableCollection<InventoryValuationGroupDto> InventoryByWarehouse { get; } = [];
    public ObservableCollection<InventoryValuationGroupDto> InventoryByCategory { get; } = [];
    public ObservableCollection<CashierSalesDto> SalesByCashier { get; } = [];
    public ObservableCollection<CategorySalesDto> SalesByCategory { get; } = [];
    public ObservableCollection<CustomerSalesDto> TopCustomers { get; } = [];

    [ObservableProperty] private bool _isBonusOpen;
    [ObservableProperty] private decimal _bonusAmount;
    [ObservableProperty] private string _bonusNote = "";
    [ObservableProperty] private CustomerSalesDto? _bonusCustomer;
    private static readonly CustomerSalesDto EmptyBonusCustomer = new(0, "", 0, 0, 0, DateTime.MinValue);
    public CustomerSalesDto BonusCustomerDisplay => BonusCustomer ?? EmptyBonusCustomer;

    partial void OnBonusCustomerChanged(CustomerSalesDto? value) => OnPropertyChanged(nameof(BonusCustomerDisplay));

    public bool CanGiveBonus => _auth.HasPermission("loyalty.manage");

    public ObservableCollection<ISeries> SalesChartSeries { get; } = [];
    [ObservableProperty] private Axis[] _salesXAxes = [new Axis()];
    [ObservableProperty] private Axis[] _salesYAxes = [new Axis { MinLimit = 0 }];

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

    public bool CanExport => _auth.HasPermission("reports.export");

    public ReportsViewModel(IReportsApi api, ICustomersApi customers, IExportService export, IToastService toast, AuthService auth)
    {
        _api = api;
        _customers = customers;
        _export = export;
        _toast = toast;
        _auth = auth;
        _auth.LoggedOut += ResetState;
    }

    public Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanGiveBonus));
        OnPropertyChanged(nameof(CanExport));
        return LoadTabAsync(SelectedTabIndex, force: true);
    }

    [RelayCommand]
    private Task LoadReportAsync()
    {
        Array.Clear(_loaded);
        return LoadTabAsync(SelectedTabIndex, force: true);
    }

    partial void OnSelectedTabIndexChanged(int value) => _ = LoadTabAsync(value);

    partial void OnDateFromChanged(DateTimeOffset value) => _ = LoadReportAsync();

    partial void OnDateToChanged(DateTimeOffset value) => _ = LoadReportAsync();

    private async Task LoadTabAsync(int index, bool force = false)
    {
        if (!force && _loaded[index]) return;
        IsLoading = true;
        try
        {
            await (index switch
            {
                0 => LoadSalesAsync(),
                1 => LoadDebtAgingAsync(),
                2 => LoadInventoryAsync(),
                3 => LoadBreakdownAsync(),
                _ => LoadTopCustomersAsync()
            });
            _loaded[index] = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ResetState()
    {
        Array.Clear(_loaded);
        TopProducts.Clear();
        DebtRows.Clear();
        InventoryByWarehouse.Clear();
        InventoryByCategory.Clear();
        SalesByCashier.Clear();
        SalesByCategory.Clear();
        TopCustomers.Clear();
        SalesChartSeries.Clear();
        TotalSales = TotalProfit = AverageSale = MaxSale = 0;
        TotalTransactions = 0;
        DebtTotal = Debt0_30 = Debt31_60 = Debt60Plus = 0;
        InventoryTotalCost = InventoryTotalRetail = 0;
        PayCash = PayCard = PayBonus = PayDebt = 0;
    }

    private DateTime FromUtc => new DateTimeOffset(DateFrom.Date).UtcDateTime;
    private DateTime ToUtc => new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
    private static int TzOffset => (int)DateTimeOffset.Now.Offset.TotalMinutes;

    private async Task LoadSalesAsync()
    {
        try
        {
            var report = await _api.GetSalesReportAsync(FromUtc, ToUtc, tzOffsetMinutes: TzOffset);

            TotalSales = report.Revenue;
            TotalProfit = report.Profit;
            TotalTransactions = report.SalesCount;
            AverageSale = report.AverageSale;
            MaxSale = report.MaxSale;

            TopProducts.Clear();
            foreach (var p in report.TopProducts) TopProducts.Add(p);

            SalesChartSeries.Clear();
            SalesChartSeries.Add(FogLine([.. report.Daily.Select(d => d.Revenue)], L["revenue"], "#166534"));
            SalesChartSeries.Add(FogLine([.. report.Daily.Select(d => d.Profit)], L["profit"], "#0EA5E9"));
            SalesXAxes = [new Axis
            {
                Labels = [.. report.Daily.Select(d => d.Date.ToString("dd/MM"))],
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")),
                TextSize = 12
            }];
            SalesYAxes = [new Axis { MinLimit = 0, LabelsPaint = new SolidColorPaint(SKColor.Parse("#94A3B8")), TextSize = 12 }];
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadDebtAgingAsync()
    {
        try
        {
            var report = await _api.GetDebtAgingReportAsync();
            DebtTotal = report.Total;
            Debt0_30 = report.Bucket0_30;
            Debt31_60 = report.Bucket31_60;
            Debt60Plus = report.Bucket60Plus;
            DebtRows.Clear();
            foreach (var r in report.Rows) DebtRows.Add(r);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadInventoryAsync()
    {
        try
        {
            var report = await _api.GetInventoryValuationReportAsync();
            InventoryTotalCost = report.TotalCost;
            InventoryTotalRetail = report.TotalRetail;
            InventoryByWarehouse.Clear();
            foreach (var g in report.ByWarehouse) InventoryByWarehouse.Add(g);
            InventoryByCategory.Clear();
            foreach (var g in report.ByCategory) InventoryByCategory.Add(g);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadBreakdownAsync()
    {
        try
        {
            var report = await _api.GetSalesBreakdownReportAsync(FromUtc, ToUtc);
            PayCash = report.Cash;
            PayCard = report.Card;
            PayBonus = report.Bonus;
            PayDebt = report.Debt;
            SalesByCashier.Clear();
            foreach (var c in report.ByCashier) SalesByCashier.Add(c);
            SalesByCategory.Clear();
            foreach (var c in report.ByCategory) SalesByCategory.Add(c);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportSales(string format)
    {
        try
        {
            var report = await _api.GetSalesReportAsync(FromUtc, ToUtc, tzOffsetMinutes: TzOffset);
            await _export.ExportAsync(L["top_products"], report.TopProducts,
            [
                new(L["product_name"], x => x.ProductName),
                new(L["quantity"], x => x.Quantity),
                new(L["revenue"], x => x.Revenue),
                new(L["profit"], x => x.Profit),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportDebt(string format)
    {
        try
        {
            var report = await _api.GetDebtAgingReportAsync();
            await _export.ExportAsync(L["debt_aging"], report.Rows,
            [
                new(L["customer"], x => x.CustomerName),
                new(L["balance"], x => x.Balance),
                new(L["last_activity"], x => x.LastActivity),
                new(L["days_overdue"], x => x.DaysOverdue),
                new(L["aging_bucket"], x => x.Bucket),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportInventory(string format)
    {
        try
        {
            var report = await _api.GetInventoryValuationReportAsync();
            await _export.ExportAsync(L["inventory_valuation"], report.ByWarehouse,
            [
                new(L["warehouse"], x => x.Name),
                new(L["quantity"], x => x.Quantity),
                new(L["cost"], x => x.Cost),
                new(L["retail"], x => x.Retail),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportBreakdown(string format)
    {
        try
        {
            var report = await _api.GetSalesBreakdownReportAsync(FromUtc, ToUtc);
            await _export.ExportAsync(L["by_cashier"], report.ByCashier,
            [
                new(L["cashier"], x => x.UserName),
                new(L["count"], x => x.Count),
                new(L["revenue"], x => x.Revenue),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadTopCustomersAsync()
    {
        try
        {
            var rows = await _api.GetTopCustomersAsync(FromUtc, ToUtc);
            TopCustomers.Clear();
            foreach (var c in rows) TopCustomers.Add(c);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ExportTopCustomers(string format)
    {
        try
        {
            var rows = await _api.GetTopCustomersAsync(FromUtc, ToUtc);
            await _export.ExportAsync(L["top_customers"], rows,
            [
                new(L["customer"], x => x.CustomerName),
                new(L["sales_count"], x => x.SalesCount),
                new(L["revenue"], x => x.Revenue),
                new(L["profit"], x => x.Profit),
                new(L["last_purchase"], x => x.LastPurchase),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenBonus(CustomerSalesDto customer)
    {
        BonusCustomer = customer;
        BonusAmount = 0;
        BonusNote = "";
        IsBonusOpen = true;
    }

    [RelayCommand]
    private void CancelBonus() => IsBonusOpen = false;

    [RelayCommand]
    private async Task GiveBonus()
    {
        if (BonusCustomer is null || BonusAmount <= 0) { _toast.Error(L["error"]); return; }
        var note = string.IsNullOrWhiteSpace(BonusNote) ? null : BonusNote.Trim();
        try
        {
            await _customers.GiveCustomerBonusAsync(BonusCustomer.CustomerId, new GiveCustomerBonusRequest(BonusAmount, note));
            IsBonusOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
