using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class HomeViewModel(
    MobileAuthService auth,
    MobilePermissions perms,
    WarehouseContext warehouse,
    CartStore cart,
    IOrderingApi ordering,
    ISalesApi sales) : ObservableObject
{
    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private bool _hasAccess = true;
    [ObservableProperty] private bool _showQueue;
    [ObservableProperty] private bool _showStats;
    [ObservableProperty] private bool _hasCart;
    [ObservableProperty] private string _cartSummary = "";
    [ObservableProperty] private int _openCarts;
    [ObservableProperty] private string _todayCountText = "";
    [ObservableProperty] private string _todayTotal = "0";
    [ObservableProperty] private string _todayTotalFull = "";
    [ObservableProperty] private bool _showTodayTotalFull;
    [ObservableProperty] private string _avgCheck = "0";
    [ObservableProperty] private List<float> _weekValues = [];
    [ObservableProperty] private List<string> _weekDays = ["", "", "", "", "", "", ""];
    [ObservableProperty] private string _weekTotal = "";
    [ObservableProperty] private bool _hasChart;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;

    public async Task AppearAsync()
    {
        HasAccess = perms.HasAny("sales.pick", "sales.create", "sales.view", "sales.viewAll");
        if (!HasAccess) return;
        ShowQueue = perms.HasAny("sales.pick", "sales.create", "sales.view");
        ShowStats = perms.HasAny("sales.view", "sales.viewAll");
        var name = auth.FullName;
        Greeting = string.Format(Loc.Instance["greeting_fmt"], name.Split(' ')[0] is { Length: > 0 } first ? first : name);
        Initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpper(x[0])));
        WarehouseName = warehouse.WarehouseName is { Length: > 0 } wh ? wh : Loc.Instance["warehouse_none"];
        HasCart = cart.Count > 0;
        CartSummary = string.Format(Loc.Instance["cart_items_fmt"], cart.Count, cart.Total.ToString("N0"));
        if (DateTime.UtcNow - _loadedAt < FreshFor) return;
        await LoadAsync();
    }

    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);
    private DateTime _loadedAt;

    private async Task LoadAsync()
    {
        Error = null;
        try
        {
            var queueTask = ShowQueue
                ? ordering.GetAllAsync("Open", warehouse.WarehouseId, "Queue")
                : Task.FromResult(new List<Cartex.Shared.Models.Ordering.CartListDto>());
            var totalsTask = ShowStats
                ? sales.GetTotalsAsync(fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(1))
                : Task.FromResult(new Cartex.Shared.Models.Sales.SalesTotalsDto(0, 0, 0, 0));
            var dailyTask = LoadDailySafeAsync();
            await Task.WhenAll(queueTask, totalsTask, dailyTask);
            if (ShowQueue) OpenCarts = queueTask.Result.Count;
            if (ShowStats)
            {
                var totals = totalsTask.Result;
                TodayCountText = string.Format(Loc.Instance["sales_count_fmt"], totals.Count);
                TodayTotal = Money.Compact(totals.TotalAmount);
                TodayTotalFull = Money.Text(totals.TotalAmount);
                ShowTodayTotalFull = totals.TotalAmount >= 1_000_000;
                AvgCheck = Money.Compact(totals.Count > 0 ? Math.Round(totals.TotalAmount / totals.Count) : 0);
                BuildWeek(dailyTask.Result);
            }
            _loadedAt = DateTime.UtcNow;
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
        }
    }

    private async Task<List<Cartex.Shared.Models.Sales.DailySalesPointDto>> LoadDailySafeAsync()
    {
        if (!ShowStats) return [];
        try
        {
            return await sales.GetDailyTotalsAsync(fromDate: DateTime.Today.AddDays(-6), toDate: DateTime.Today.AddDays(1));
        }
        catch
        {
            return [];
        }
    }

    private void BuildWeek(List<Cartex.Shared.Models.Sales.DailySalesPointDto> points)
    {
        var names = Loc.Instance["days_short"].Split(',');
        var byDay = points.ToDictionary(x => x.Date.Date, x => x.TotalAmount);
        var values = new List<float>(7);
        var days = new List<string>(7);
        decimal sum = 0;
        for (var i = 6; i >= 0; i--)
        {
            var date = DateTime.Today.AddDays(-i);
            var amount = byDay.GetValueOrDefault(date);
            sum += amount;
            values.Add((float)amount);
            days.Add(names.Length == 7 ? names[((int)date.DayOfWeek + 6) % 7] : date.Day.ToString());
        }
        WeekValues = values;
        WeekDays = days;
        WeekTotal = string.Format(Loc.Instance["week_total_fmt"], Money.Compact(sum));
        HasChart = sum > 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task NewCartAsync() => Shell.Current.GoToAsync("//main/scan");

    [RelayCommand]
    private Task OpenCartAsync() => Shell.Current.GoToAsync("cart");

    [RelayCommand]
    private Task OpenQueueAsync() => Shell.Current.GoToAsync("//main/trade");

    [RelayCommand]
    private Task OpenSalesAsync() => Shell.Current.GoToAsync("//main/trade?section=sales");

    [RelayCommand]
    private Task OpenCustomersAsync() => Shell.Current.GoToAsync("//main/customers");

    [RelayCommand]
    private Task OpenProfileAsync() => Shell.Current.GoToAsync("//main/profile");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        cart.Clear();
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
