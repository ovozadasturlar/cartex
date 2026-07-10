using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class DevicesViewModel(ISessionsApi sessionsApi, MobileAuthService auth) : ObservableObject
{
    public ObservableCollection<DeviceRow> Devices { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string? _error;

    public async Task AppearAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var sessions = await sessionsApi.GetSessionsAsync();
            Devices.Clear();
            foreach (var s in sessions.OrderByDescending(s => s.LastUsedAt))
                Devices.Add(new DeviceRow(s, s.DeviceName == auth.DeviceName));
            IsEmpty = Devices.Count == 0;
        }
        catch (Exception ex)
        {
            Error = ex is Refit.ApiException api ? SyncService.DescribeError(api) : Loc.Instance["err_no_connection"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task OpenScanAsync() => Shell.Current.GoToAsync("scan");

    [RelayCommand]
    private async Task RevokeAsync(DeviceRow row)
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlert(Loc.Instance["device_revoke"],
                string.Format(Loc.Instance["device_revoke_confirm"], row.Name),
                Loc.Instance["device_revoke"], Loc.Instance["cancel"]))
            return;
        try
        {
            await sessionsApi.RevokeSessionAsync(row.Session.Id);
            Ui.Toast(Loc.Instance["device_revoked"]);
            await AppearAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? SyncService.DescribeError(api) : Loc.Instance["err_no_connection"]);
        }
    }
}

public sealed record DeviceRow(DeviceSessionDto Session, bool IsCurrent)
{
    public string Name => Session.DeviceName is { Length: > 0 } n ? n : Loc.Instance["device_unknown"];
    public string SubLine => $"{Loc.Instance["last_active"]}{Session.LastUsedAt.ToLocalTime():dd.MM.yyyy HH:mm}";
    public string Initial => Name[..1].ToUpper();
    public bool CanRevoke => !IsCurrent;
}
