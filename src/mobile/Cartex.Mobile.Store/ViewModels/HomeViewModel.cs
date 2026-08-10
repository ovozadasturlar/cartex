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
    [ObservableProperty] private bool _hasAccess = true;
    [ObservableProperty] private bool _showQueue;
    [ObservableProperty] private bool _showStats;
    [ObservableProperty] private bool _hasCart;
    [ObservableProperty] private string _cartSummary = "";
    [ObservableProperty] private int _openCarts;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayTotal = "0";
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
        WarehouseName = warehouse.WarehouseName is { Length: > 0 } wh ? wh : Loc.Instance["warehouse_none"];
        HasCart = cart.Count > 0;
        CartSummary = string.Format(Loc.Instance["cart_items_fmt"], cart.Count, cart.Total.ToString("N0"));
        await LoadAsync();
    }

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
            await Task.WhenAll(queueTask, totalsTask);
            if (ShowQueue) OpenCarts = queueTask.Result.Count;
            if (ShowStats)
            {
                TodayCount = totalsTask.Result.Count;
                TodayTotal = totalsTask.Result.TotalAmount.ToString("N0");
            }
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
        }
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
    private async Task LogoutAsync()
    {
        cart.Clear();
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
