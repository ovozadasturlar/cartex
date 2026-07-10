using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ProfileViewModel(MobileAuthService auth, AgentDb db, SyncService sync, SessionStore session, IBiometricAuth biometric) : ObservableObject
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _languageName = "";
    [ObservableProperty] private string _outboxBadge = "";
    [ObservableProperty] private string _footer = "";
    [ObservableProperty] private string _pinStatus = "";
    [ObservableProperty] private bool _bioEnabled;
    [ObservableProperty] private bool _bioAllowed;
    [ObservableProperty] private bool _hasOutbox;
    [ObservableProperty] private bool _isBusy;

    private static readonly string[] LangNames = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];
    private static readonly string[] LangCodes = ["uz-latn", "uz-cyrl", "ru", "en"];

    public async Task AppearAsync()
    {
        FullName = auth.FullName is { Length: > 0 } name ? name : "—";
        Initials = string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        var warehouse = await db.GetMetaAsync("warehouse_name");
        Subtitle = string.IsNullOrEmpty(warehouse) ? auth.Role : $"{auth.Role} • {warehouse}";
        LanguageName = LangNames[Math.Max(0, Array.IndexOf(LangCodes, Loc.Instance.Language))];
        var pending = await db.CountOutboxAsync("pending");
        var errors = await db.CountOutboxAsync("error");
        HasOutbox = pending + errors > 0;
        OutboxBadge = errors > 0 ? $"{pending + errors}!" : pending.ToString();
        Footer = $"Cartex Agent {AppInfo.Current.VersionString} • {session.ServerUrl}";
        PinStatus = Loc.Instance[AppLock.PinEnabled ? "enabled" : "disabled"];
        BioAllowed = AppLock.PinEnabled && biometric.IsAvailable;
        _suppressBio = true;
        BioEnabled = AppLock.BiometricEnabled;
        _suppressBio = false;
    }

    private bool _suppressBio;

    partial void OnBioEnabledChanged(bool value)
    {
        if (_suppressBio) return;
        if (value && (!AppLock.PinEnabled || !biometric.IsAvailable))
        {
            _suppressBio = true;
            BioEnabled = false;
            _suppressBio = false;
            Ui.Toast(Loc.Instance["biometric_unavailable"]);
            return;
        }
        AppLock.SetBiometric(value);
    }

    [RelayCommand]
    private async Task TogglePinAsync()
    {
        var page = Shell.Current.CurrentPage;
        if (!AppLock.PinEnabled)
        {
            await Shell.Current.GoToAsync("pin?setup=1");
            return;
        }
        var disable = Loc.Instance["pin_disable"];
        var change = Loc.Instance["pin_change"];
        var choice = await page.DisplayActionSheet(Loc.Instance["pin_code"], Loc.Instance["cancel"], null, change, disable);
        if (choice == change)
            await Shell.Current.GoToAsync("pin?setup=1");
        else if (choice == disable)
        {
            AppLock.Disable();
            await AppearAsync();
        }
    }

    [RelayCommand]
    private Task OpenDevicesAsync() => Shell.Current.GoToAsync("devices");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("outbox");

    [RelayCommand]
    private Task ChangePasswordAsync() => Shell.Current.GoToAsync("change-password");

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await sync.SyncAsync();
            await AppearAsync();
            Ui.Toast(Loc.Instance[sync.IsOffline ? "offline_banner" : "sync_done"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChooseLanguageAsync()
    {
        var choice = await Shell.Current.CurrentPage.DisplayActionSheet(
            Loc.Instance["language"], Loc.Instance["cancel"], null, LangNames);
        var index = Array.IndexOf(LangNames, choice);
        if (index < 0) return;
        await Loc.Instance.SetLanguageAsync(LangCodes[index]);
        await AppearAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var page = Shell.Current.CurrentPage;
        if (await db.CountOutboxAsync("pending") > 0)
        {
            await page.DisplayAlert(Loc.Instance["logout_blocked_title"], Loc.Instance["logout_blocked_msg"], Loc.Instance["ok"]);
            return;
        }
        if (!await page.DisplayAlert(Loc.Instance["logout"], Loc.Instance["logout_confirm"], Loc.Instance["logout"], Loc.Instance["cancel"]))
            return;
        AppLock.Disable();
        await auth.LogoutAsync();
        await db.ClearCacheAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
