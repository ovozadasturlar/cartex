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
        WarehouseName = await db.GetMetaAsync("warehouse_name") is { Length: > 0 } name ? name : "Ombor biriktirilmagan";
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
            ShiftText = shift is null ? "Yopiq" : $"Ochiq — {shift.OpenedAt.ToLocalTime():HH:mm} dan";
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
                var input = await page.DisplayPromptAsync("Smenani yopish", "Sanalgan naqd pul:", "Yopish", "Bekor", keyboard: Keyboard.Numeric);
                if (input is null) return;
                if (!decimal.TryParse(input, out var counted)) { Ui.Toast("Summa noto'g'ri"); return; }
                var report = await shiftsApi.CloseAsync(id, new CloseShiftRequest(counted));
                await page.DisplayAlert("Smena yopildi",
                    $"Kutilgan: {report.ExpectedCash:N0}\nSanalgan: {report.CountedCash:N0}\nFarq: {report.Difference:N0}", "OK");
            }
            else
            {
                await shiftsApi.OpenAsync(new OpenShiftRequest(0));
                Ui.Toast("Smena ochildi");
            }
            await LoadShiftAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? SyncService.DescribeError(api) : "Serverga ulanib bo'lmadi");
        }
    }

    [RelayCommand]
    private Task NewSaleAsync() => Shell.Current.GoToAsync("sale");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("//outbox");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (await db.CountOutboxAsync("pending") > 0)
        {
            await Shell.Current.CurrentPage.DisplayAlert("Chiqib bo'lmaydi",
                "Navbatda yuborilmagan amallar bor. Avval sinxronlang.", "OK");
            return;
        }
        await auth.LogoutAsync();
        await db.ClearCacheAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
