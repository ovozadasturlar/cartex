using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class HomeViewModel(
    MobileAuthService auth,
    MobilePermissions perms,
    WarehouseContext warehouse,
    CartStore cart,
    SupplyCartStore supplyCart,
    IOrderingApi ordering,
    ISalesApi sales,
    SalesPolicyCache policy,
    MobileFeaturesCache features,
    SessionStore session) : ObservableObject
{
    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private bool _hasAccess = true;
    [ObservableProperty] private bool _showQueue;
    [ObservableProperty] private bool _showStats;
    [ObservableProperty] private bool _hasCart;
    [ObservableProperty] private int _cartCount;
    [ObservableProperty] private int _supplyCount;
    [ObservableProperty] private bool _canReceiveStock;
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
        await session.LoadAsync();
        HasAccess = perms.HasAny("sales.pick", "sales.create", "sales.view", "sales.viewAll");
        if (!HasAccess) return;
        ShowStats = perms.HasAny("sales.view", "sales.viewAll");
        var name = auth.FullName;
        Greeting = string.Format(Loc.Instance["greeting_fmt"], name.Split(' ')[0] is { Length: > 0 } first ? first : name);
        Initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpper(x[0])));
        WarehouseName = warehouse.WarehouseName is { Length: > 0 } wh ? wh : Loc.Instance["warehouse_none"];
        CanReceiveStock = perms.Has("supplies.create");
        HasCart = cart.Count > 0;
        CartCount = cart.Count;
        SupplyCount = supplyCart.Count;
        CartSummary = string.Format(Loc.Instance["cart_items_fmt"], cart.Count, cart.Total.ToString("N0"));
        // NAVBAT-06: navbat do'kon siyosati yoki modul bilan o'chirilgan bo'lsa, ilovada u
        // haqda hech narsa ko'rinmasligi kerak — shuning uchun ko'rinish shu ikkisi
        // yuklangandan keyin hisoblanadi.
        await Task.WhenAll(policy.RefreshAsync(), features.RefreshAsync());
        ShowQueue = perms.HasAny("sales.pick", "sales.view")
            && policy.Current.AllowSaleQueue && features.QueueEnabled;
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
                ? Task.Run(() => ordering.GetAllAsync("Open", warehouse.WarehouseId, "Queue"))
                : Task.FromResult(new List<Cartex.Shared.Models.Ordering.CartListDto>());
            var totalsTask = ShowStats
                ? Task.Run(() => sales.GetTotalsAsync(fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(1)))
                : Task.FromResult(new Cartex.Shared.Models.Sales.SalesTotalsDto(0, 0, 0, 0));
            var dailyTask = Task.Run(LoadDailySafeAsync);
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
        catch (Exception ex)
        {
            Error = ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"];
        }
    }

    private async Task<List<Cartex.Shared.Models.Sales.DailySalesPointDto>> LoadDailySafeAsync()
    {
        if (!ShowStats) return [];
        try
        {
            // Chegaralar mahalliy kun boshidan olinadi, lekin serverga instant sifatida yuboriladi —
            // aks holda oyna mintaqa farqicha siljib, birinchi va oxirgi kun to'liq chiqmaydi.
            return await sales.GetDailyTotalsAsync(
                fromDate: DateTime.Today.AddDays(-6).ToUniversalTime(),
                toDate: DateTime.Today.AddDays(1).ToUniversalTime(),
                tzOffsetMinutes: (int)DateTimeOffset.Now.Offset.TotalMinutes);
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
        if (!values.SequenceEqual(WeekValues))
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
    private void NewCart() => Views.MainPage.Current?.Show(2);

    [RelayCommand]
    private Task OpenCartAsync() => Shell.Current.GoToAsync("cart");

    [RelayCommand]
    private void OpenQueue() => Views.MainPage.Current?.Show(1);

    [RelayCommand]
    private void OpenSales() => Views.MainPage.Current?.Show(1, "sales");

    [RelayCommand]
    private Task OpenSupplyCartAsync() => Shell.Current.GoToAsync("receive_cart");

    [RelayCommand]
    private void OpenCustomers() => Views.MainPage.Current?.Show(3);

    [RelayCommand]
    private void OpenProfile() => Views.MainPage.Current?.Show(4);

    [RelayCommand]
    private async Task LogoutAsync()
    {
        cart.Clear();
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
