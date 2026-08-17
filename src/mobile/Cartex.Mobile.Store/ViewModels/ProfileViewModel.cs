using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProfileViewModel(
    MobileAuthService auth,
    SessionStore session,
    CartStore cart,
    WarehouseContext warehouseContext,
    MobileOfflineService offline,
    IOfflineCacheApi offlineApi,
    OrderingHubService orderingHub,
    MobilePermissions permissions) : ObservableObject
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _languageName = "";
    [ObservableProperty] private string _footer = "";
    [ObservableProperty] private string _themeName = "";
    [ObservableProperty] private bool _offlineVisible;
    [ObservableProperty] private bool _offlineBusy;
    [ObservableProperty] private string _offlineTitle = "";
    [ObservableProperty] private string _offlineSubtitle = "";
    [ObservableProperty] private string _offlineAction = "";
    private OfflineCacheStateDto? _offlineState;

    private static readonly string[] LangNames = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];
    private static readonly string[] LangCodes = ["uz-latn", "uz-cyrl", "ru", "en"];

    private DateTime _offlineLoadedAt;

    public async Task AppearAsync()
    {
        FullName = auth.FullName is { Length: > 0 } name ? name : "—";
        Initials = string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        Subtitle = auth.Role;
        WarehouseName = warehouseContext.WarehouseName is { Length: > 0 } wh ? wh : "—";
        LanguageName = LangNames[Math.Max(0, Array.IndexOf(LangCodes, Loc.Instance.Language))];
        Footer = $"Cartex Do'kon {AppInfo.Current.VersionString} • {session.ServerUrl}";
        ThemeName = Loc.Instance["theme_" + Preferences.Get("app_theme", "system")];
        OfflineVisible = permissions.Has("devices.revoke") && permissions.HasAny("sales.create", "sales.checkout");
        if (!OfflineVisible) return;
        if (_offlineState is not null && DateTime.UtcNow - _offlineLoadedAt < TimeSpan.FromSeconds(30)) return;
        await offline.StartAsync();
        await RefreshOfflineAsync();
        _offlineLoadedAt = DateTime.UtcNow;
    }

    [RelayCommand]
    private async Task ChangeWarehouseAsync()
    {
        await warehouseContext.ChangeAsync();
        await AppearAsync();
    }

    [RelayCommand]
    private Task OpenDevicesAsync() => Shell.Current.GoToAsync("devices");

    [RelayCommand]
    private Task OpenChangePasswordAsync() => Shell.Current.GoToAsync("change-password");

    [RelayCommand]
    private Task OpenSecurityAsync() => Shell.Current.GoToAsync("security");

    [RelayCommand]
    private async Task ChooseThemeAsync()
    {
        string[] keys = ["system", "light", "dark"];
        var names = keys.Select(k => Loc.Instance["theme_" + k]).ToArray();
        var choice = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            Loc.Instance["theme"], Loc.Instance["cancel"], null, names);
        var index = Array.IndexOf(names, choice);
        if (index < 0) return;
        Preferences.Set("app_theme", keys[index]);
        ApplyTheme();
        await AppearAsync();
    }

    public static void ApplyTheme() =>
        Application.Current!.UserAppTheme = Preferences.Get("app_theme", "system") switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

    [RelayCommand]
    private async Task ChooseLanguageAsync()
    {
        var choice = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            Loc.Instance["language"], Loc.Instance["cancel"], null, LangNames);
        var index = Array.IndexOf(LangNames, choice);
        if (index < 0) return;
        await Loc.Instance.SetLanguageAsync(LangCodes[index]);
        var window = Application.Current!.Windows[0];
        window.Page = new AppShell();
        await Shell.Current.GoToAsync("//profile");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync(Loc.Instance["logout"], Loc.Instance["logout_confirm"], Loc.Instance["logout"], Loc.Instance["cancel"]))
            return;
        AppLock.Disable();
        await auth.LogoutAsync();
        await orderingHub.StopAsync();
        cart.Clear();
        warehouseContext.Reset();
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task ToggleOfflineAsync()
    {
        if (OfflineBusy) return;
        OfflineBusy = true;
        try
        {
            if (offline.IsEnabled && _offlineState?.IsCurrentDevice == true)
            {
                var pending = await offline.PendingCountAsync();
                var errors = await offline.ErrorCountAsync();
                if (pending + errors > 0 && !await Shell.Current.CurrentPage.DisplayAlertAsync(
                        Loc.Instance["offline_sales"],
                        string.Format(Loc.Instance["offline_pending_release_fmt"], pending + errors),
                        Loc.Instance["disconnect"], Loc.Instance["cancel"]))
                    return;
                await offline.ReleaseAsync(pending + errors > 0
                    ? "Mobil qurilmadan sinxronlanmagan amallar bilan uzildi"
                    : null);
            }
            else
            {
                _offlineState = await Task.Run(offlineApi.GetStateAsync);
                if (_offlineState.DeviceId is not null && !_offlineState.IsCurrentDevice)
                {
                    var message = string.Format(Loc.Instance["offline_other_device_fmt"],
                        _offlineState.DeviceName ?? "—", _offlineState.LastReportedPendingCount);
                    if (!await Shell.Current.CurrentPage.DisplayAlertAsync(
                            Loc.Instance["offline_sales"], message,
                            Loc.Instance["disconnect"], Loc.Instance["cancel"]))
                        return;
                    await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
                        _offlineState.LeaseId, Force: true,
                        Reason: "Mobil qurilmadan yangi vakolat olish uchun majburan uzildi"));
                }

                if (!await warehouseContext.EnsureSelectedAsync() || warehouseContext.WarehouseId is null)
                    throw new InvalidOperationException(Loc.Instance["warehouse_none"]);
                var grant = await offlineApi.ClaimAsync(new ClaimOfflineCacheRequest(
                    auth.DeviceId, auth.DeviceName, warehouseContext.WarehouseId.Value));
                await offline.ActivateAsync(grant);
            }
            await RefreshOfflineAsync();
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? ApiErrors.Describe(api) : ex.Message);
        }
        finally
        {
            OfflineBusy = false;
        }
    }

    private async Task RefreshOfflineAsync()
    {
        try
        {
            _offlineState = await Task.Run(offlineApi.GetStateAsync);
            if (_offlineState.DeviceId is null)
            {
                OfflineTitle = Loc.Instance["offline_sales"];
                OfflineSubtitle = Loc.Instance["offline_available"];
                OfflineAction = Loc.Instance["enable"];
            }
            else if (_offlineState.IsCurrentDevice)
            {
                var pending = await offline.PendingCountAsync();
                var errors = await offline.ErrorCountAsync();
                var last = await offline.LastSyncAsync() ?? "—";
                OfflineTitle = $"{Loc.Instance["offline_sales"]} · {Loc.Instance["enabled"]}";
                OfflineSubtitle = string.Format(Loc.Instance["offline_status_fmt"],
                    _offlineState.WarehouseName ?? warehouseContext.WarehouseName,
                    pending, errors, last);
                OfflineAction = Loc.Instance["disconnect"];
            }
            else
            {
                OfflineTitle = Loc.Instance["offline_sales"];
                OfflineSubtitle = string.Format(Loc.Instance["offline_holder_fmt"],
                    _offlineState.DeviceName ?? "—", _offlineState.WarehouseName ?? "—",
                    _offlineState.LastReportedPendingCount);
                OfflineAction = Loc.Instance["take_over"];
            }
        }
        catch
        {
            OfflineTitle = Loc.Instance["offline_sales"];
            OfflineSubtitle = offline.IsEnabled
                ? Loc.Instance["offline_local_ready"]
                : Loc.Instance["err_no_connection"];
            OfflineAction = offline.IsEnabled ? Loc.Instance["disconnect"] : Loc.Instance["enable"];
        }
    }
}
