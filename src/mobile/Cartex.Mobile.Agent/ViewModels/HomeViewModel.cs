using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Shifts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class HomeViewModel(SyncService sync, AgentDb db, MobileAuthService auth, IShiftsApi shiftsApi) : ObservableObject
{
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _lastSync = "—";
    [ObservableProperty] private int _customerCount;
    [ObservableProperty] private int _stockCount;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _errorCount;
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
        WarehouseName = await db.GetMetaAsync("warehouse_name") is { Length: > 0 } name ? name : Loc.Instance["warehouse_none"];
        LastSync = await db.GetMetaAsync("last_sync") ?? "—";
        CustomerCount = await db.CountAsync<LocalCustomer>();
        StockCount = await db.CountAsync<LocalVanStock>();
        PendingCount = await db.CountOutboxAsync("pending");
        ErrorCount = await db.CountOutboxAsync("error");
        IsOffline = sync.IsOffline;
        Error = sync.LastError;
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
    private async Task ChooseLanguageAsync()
    {
        string[] names = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];
        string[] codes = ["uz-latn", "uz-cyrl", "ru", "en"];
        var choice = await Shell.Current.CurrentPage.DisplayActionSheet(
            Loc.Instance["language"], Loc.Instance["cancel"], null, names);
        var index = Array.IndexOf(names, choice);
        if (index < 0) return;
        await Loc.Instance.SetLanguageAsync(codes[index]);
        await LoadLocalAsync();
        await LoadShiftAsync();
    }

    [RelayCommand]
    private Task NewSaleAsync() => Shell.Current.GoToAsync("sale");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("//outbox");

    [RelayCommand]
    private Task OpenScanAsync() => Shell.Current.GoToAsync("scan");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (await db.CountOutboxAsync("pending") > 0)
        {
            await Shell.Current.CurrentPage.DisplayAlert(Loc.Instance["logout_blocked_title"],
                Loc.Instance["logout_blocked_msg"], Loc.Instance["ok"]);
            return;
        }
        await auth.LogoutAsync();
        await db.ClearCacheAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
