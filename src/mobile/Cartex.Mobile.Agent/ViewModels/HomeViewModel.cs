using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Shifts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class HomeViewModel(SyncService sync, AgentDb db, MobileAuthService auth, IShiftsApi shiftsApi) : ObservableObject
{
    public ObservableCollection<VisitRow> Visits { get; } = [];

    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _lastSync = "—";
    [ObservableProperty] private int _customerCount;
    [ObservableProperty] private int _pendingDeliveries;
    [ObservableProperty] private int _yesterdayOrders;
    [ObservableProperty] private int _yesterdayDelivered;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private bool _visitsEmpty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isOffline;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _shiftText = "—";
    [ObservableProperty] private bool _shiftOpen;

    private long? _shiftId;

    public async Task AppearAsync()
    {
        await LoadLocalAsync();
        if (sync.LastAttempt is null || DateTime.Now - sync.LastAttempt > TimeSpan.FromMinutes(2))
            await SyncCommand.ExecuteAsync(null);
        else
            await LoadShiftAsync();
    }

    private async Task LoadLocalAsync()
    {
        var name = auth.FullName;
        Greeting = string.Format(Loc.Instance["greeting_fmt"], name.Split(' ')[0] is { Length: > 0 } first ? first : name);
        WarehouseName = await db.GetMetaAsync("warehouse_name") is { Length: > 0 } wh ? wh : Loc.Instance["warehouse_none"];
        LastSync = await db.GetMetaAsync("last_sync") ?? "—";
        CustomerCount = await db.CountAsync<LocalCustomer>();
        ErrorCount = await db.CountOutboxAsync("error");
        IsOffline = sync.IsOffline;
        Error = sync.LastError;

        var orders = await db.GetOrdersAsync();
        var yesterday = DateTime.Today.AddDays(-1);
        var pending = orders.Where(o => o.Status != "delivered").ToList();
        PendingDeliveries = pending.Count;
        YesterdayOrders = orders.Count(o => o.CreatedAt.Date == yesterday);
        YesterdayDelivered = orders.Count(o => o.DeliveredAt?.Date == yesterday);

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
            await LoadShiftAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadShiftAsync()
    {
        try
        {
            var shift = await shiftsApi.GetCurrentAsync();
            _shiftId = shift?.Id;
            ShiftOpen = shift is not null;
            ShiftText = shift is null
                ? Loc.Instance["shift_closed"]
                : string.Format(Loc.Instance["shift_open_fmt"], shift.OpenedAt.ToLocalTime().ToString("HH:mm"));
        }
        catch
        {
            _shiftId = null;
            ShiftText = "—";
        }
    }

    [RelayCommand]
    private async Task ToggleShiftAsync()
    {
        var page = Shell.Current.CurrentPage;
        try
        {
            if (ShiftOpen && _shiftId is { } id)
            {
                var input = await page.DisplayPromptAsync(Loc.Instance["shift_close_title"], Loc.Instance["counted_cash"],
                    Loc.Instance["shift_close_btn"], Loc.Instance["cancel"], keyboard: Keyboard.Numeric);
                if (input is null) return;
                if (!decimal.TryParse(input, out var counted)) { Ui.Toast(Loc.Instance["amount_invalid"]); return; }
                var report = await shiftsApi.CloseAsync(id, new CloseShiftRequest(counted));
                await page.DisplayAlert(Loc.Instance["shift_closed_title"],
                    string.Format(Loc.Instance["shift_report_fmt"], report.ExpectedCash, report.CountedCash, report.Difference),
                    Loc.Instance["ok"]);
            }
            else
            {
                await shiftsApi.OpenAsync(new OpenShiftRequest(0));
                Ui.Toast(Loc.Instance["shift_opened"]);
            }
            await LoadShiftAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? SyncService.DescribeError(api) : Loc.Instance["err_no_connection"]);
        }
    }

    [RelayCommand]
    private Task NewOrderAsync() => Shell.Current.GoToAsync("order");

    [RelayCommand]
    private Task NewSaleAsync() => Shell.Current.GoToAsync("sale");

    [RelayCommand]
    private Task OpenOrdersAsync() => Shell.Current.GoToAsync("//orders");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("outbox");
}

public sealed record VisitRow(string Name, int OrderCount, decimal Total)
{
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
    public string SubLine => $"{OrderCount} {Loc.Instance["orders_short"]} • {Total:N0}";
}
