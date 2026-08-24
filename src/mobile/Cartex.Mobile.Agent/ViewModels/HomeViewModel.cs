using System.Collections.ObjectModel;
using System.Globalization;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class HomeViewModel(
    SyncService sync,
    AgentDb db,
    MobileAuthService auth,
    AccessState access) : AccessAwareViewModel(access)
{
    public ObservableCollection<VisitRow> Visits { get; } = [];
    public ObservableCollection<DayBar> WeekBars { get; } = [];

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _lastSync = "—";
    [ObservableProperty] private int _customerCount;
    [ObservableProperty] private int _pendingDeliveries;
    [ObservableProperty] private int _yesterdayOrders;
    [ObservableProperty] private int _yesterdayDelivered;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private string _week7Total = "0";
    [ObservableProperty] private string _vanValue = "0";
    [ObservableProperty] private bool _visitsEmpty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _error;

    public bool IsOffline => sync.IsOffline || Access.IsStale;
    public string OfflineText => Access.IsStale
        ? string.Format(Loc.Instance["access_stale_fmt"], Access.LastSuccessfulRefresh?.ToLocalTime().ToString("HH:mm") ?? "—")
        : Loc.Instance["offline_banner"];

    public async Task AppearAsync()
    {
        ObserveAccess(nameof(IsOffline), nameof(OfflineText));
        await Access.EnsureLoadedAsync();
        await LoadLocalAsync();
        if (sync.LastAttempt is null || DateTime.Now - sync.LastAttempt > TimeSpan.FromMinutes(2))
            await SyncCommand.ExecuteAsync(null);
    }

    private async Task LoadLocalAsync()
    {
        var name = auth.FullName;
        Greeting = string.Format(Loc.Instance["greeting_fmt"], name.Split(' ')[0] is { Length: > 0 } first ? first : name);
        WarehouseName = await db.GetMetaAsync("warehouse_name") is { Length: > 0 } wh ? wh : Loc.Instance["warehouse_none"];
        LastSync = await db.GetMetaAsync("last_sync") ?? "—";
        CustomerCount = await db.CountAsync<LocalCustomer>();
        ErrorCount = await db.CountOutboxAsync("error");
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(OfflineText));
        Error = sync.LastError;

        var orders = await db.GetOrdersAsync();
        var yesterday = DateTime.Today.AddDays(-1);
        var pending = orders.Where(o => o.Status != "delivered").ToList();
        PendingDeliveries = pending.Count;
        YesterdayOrders = orders.Count(o => o.CreatedAt.Date == yesterday);
        YesterdayDelivered = orders.Count(o => o.DeliveredAt?.Date == yesterday);

        var stock = await db.GetVanStockAsync();
        VanValue = stock.Sum(s => s.Quantity * s.SellingPrice).ToString("N0");

        var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToList();
        var sums = days.Select(d => orders.Where(o => o.CreatedAt.Date == d).Sum(o => o.Total)).ToList();
        var max = sums.Max();
        Week7Total = sums.Sum().ToString("N0");
        WeekBars.Clear();
        for (var i = 0; i < 7; i++)
            WeekBars.Add(new DayBar(
                days[i].ToString("ddd", CultureInfo.CurrentUICulture)[..1].ToUpperInvariant(),
                max == 0 ? 6 : 6 + (double)(sums[i] / max) * 66,
                max > 0 && sums[i] == max));

        Visits.Clear();
        foreach (var g in pending.GroupBy(o => o.CustomerName).OrderBy(g => g.Key))
            Visits.Add(new VisitRow(g.Key, g.Count(), g.Sum(o => o.Total)));
        VisitsEmpty = Visits.Count == 0;
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await sync.SyncAsync();
            await LoadLocalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await SyncAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private Task NewOrderAsync() => Shell.Current.GoToAsync("order");

    [RelayCommand]
    private Task NewSaleAsync() => Shell.Current.GoToAsync("sale");

    [RelayCommand]
    private Task OpenOrdersAsync() => Shell.Current.GoToAsync("//orders");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("outbox");

    [RelayCommand]
    private Task OpenStockAsync() => Shell.Current.GoToAsync("vanstock");

    [RelayCommand]
    private Task OpenDaySummaryAsync() => Shell.Current.GoToAsync("day-summary");

    [RelayCommand]
    private Task OpenRouteAsync() => Shell.Current.GoToAsync("visit-route");
}

public sealed record DayBar(string Day, double Height, bool IsMax);

public sealed record VisitRow(string Name, int OrderCount, decimal Total)
{
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
    public string SubLine => $"{OrderCount} {Loc.Instance["orders_short"]} • {Total:N0}";
}
