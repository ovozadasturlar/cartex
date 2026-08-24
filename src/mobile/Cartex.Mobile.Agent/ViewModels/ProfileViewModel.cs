using Cartex.Mobile.Agent.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class ProfileViewModel(MobileAuthService auth, AccessState access, AgentDb db, SessionStore session) : ObservableObject
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _languageName = "";
    [ObservableProperty] private string _outboxBadge = "";
    [ObservableProperty] private string _footer = "";
    [ObservableProperty] private string _themeName = "";
    [ObservableProperty] private bool _hasOutbox;

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
        ThemeName = Loc.Instance["theme_" + Preferences.Get("app_theme", "system")];
    }

    [RelayCommand]
    private Task OpenDevicesAsync() => Shell.Current.GoToAsync("devices");

    [RelayCommand]
    private Task OpenHomeAsync() => Shell.Current.GoToAsync("home");

    [RelayCommand]
    private Task OpenVanStockAsync() => Shell.Current.GoToAsync("vanstock");

    [RelayCommand]
    private Task OpenTransfersAsync() => Shell.Current.GoToAsync("transfers");

    [RelayCommand]
    private Task OpenOutboxAsync() => Shell.Current.GoToAsync("outbox");

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
        if (await db.CountOutboxAsync("pending") > 0)
        {
            await page.DisplayAlertAsync(Loc.Instance["logout_blocked_title"], Loc.Instance["logout_blocked_msg"], Loc.Instance["ok"]);
            return;
        }
        if (!await page.DisplayAlertAsync(Loc.Instance["logout"], Loc.Instance["logout_confirm"], Loc.Instance["logout"], Loc.Instance["cancel"]))
            return;
        AppLock.Disable();
        await auth.LogoutAsync();
        access.Clear();
        await db.ClearCacheAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
