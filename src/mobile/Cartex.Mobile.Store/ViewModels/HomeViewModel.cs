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
    [ObservableProperty] private bool _canSell;
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
        CartCount = cart.Count;
        SupplyCount = supplyCart.Count;
        CartSummary = string.Format(Loc.Instance["cart_items_fmt"], cart.Count, cart.Total.ToString("N0"));
        // NAVBAT-06: navbat do'kon siyosati yoki modul bilan o'chirilgan bo'lsa, ilovada u
        // haqda hech narsa ko'rinmasligi kerak — shuning uchun ko'rinish shu ikkisi
        // yuklangandan keyin hisoblanadi.
        await Task.WhenAll(policy.RefreshAsync(), features.RefreshAsync());
        ShowQueue = perms.HasAny("sales.pick", "sales.view")
            && policy.Current.AllowSaleQueue && features.CartsEnabled;
        // RUXSAT-04: bu ilovada savdo savat orqali ketadi, ya'ni u modul o'chiq bo'lsa umuman
        // mumkin emas. Shunday holatda savdo tugmalari ko'rsatilmaydi — aks holda kassir savat
        // yig'ib, faqat oxirida "ruxsat yo'q" degan javob olardi.
        CanSell = perms.HasAny("sales.create", "sales.checkout") && features.CartsEnabled;
        HasCart = CanSell && cart.Count > 0;
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
            // HIS-06: "tushum" — qaytarilgan qism chiqarilgan sof qiymat, boshqaruv paneli bilan
            // bir xil ta'rifda. So'rov `sales.view` ostida ketadi: shu karta ko'rinadigan ruxsat
            // bilan bir xil, ya'ni `reports` moduli o'chirilgan do'konda ham ishlaydi.
            var dailyTask = ShowStats
                ? Task.Run(() => sales.GetDailyTotalsAsync(
                    warehouse.WarehouseId,
                    DateTime.Today.AddDays(-6).ToUniversalTime(),
                    DateTime.Today.AddDays(1).ToUniversalTime(),
                    (int)DateTimeOffset.Now.Offset.TotalMinutes))
                : Task.FromResult(new List<Cartex.Shared.Models.Sales.DailySalesPointDto>());
            await Task.WhenAll(queueTask, dailyTask);
            if (ShowQueue) OpenCarts = queueTask.Result.Count;
            if (ShowStats)
            {
                var daily = dailyTask.Result;
                var today = daily.FirstOrDefault(x => x.Date.Date == DateTime.Today);
                var revenue = today?.TotalAmount ?? 0;
                var count = today?.Count ?? 0;
                TodayCountText = string.Format(Loc.Instance["sales_count_fmt"], count);
                TodayTotal = Money.Compact(revenue);
                TodayTotalFull = Money.Text(revenue);
                ShowTodayTotalFull = revenue >= 1_000_000;
                AvgCheck = Money.Compact(count > 0 ? Math.Round(revenue / count) : 0);
                BuildWeek(daily);
            }
            _loadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Error = ex is ApiException api ? ApiErrors.Describe(api) : Loc.Instance["err_no_connection"];
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
