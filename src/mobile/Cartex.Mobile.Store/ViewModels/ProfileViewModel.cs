using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProfileViewModel(MobileAuthService auth, SessionStore session, CartStore cart, WarehouseContext warehouseContext) : ObservableObject
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string _warehouseName = "";
    [ObservableProperty] private string _languageName = "";
    [ObservableProperty] private string _footer = "";
    [ObservableProperty] private string _themeName = "";

    private static readonly string[] LangNames = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];
    private static readonly string[] LangCodes = ["uz-latn", "uz-cyrl", "ru", "en"];

    public void Appear()
    {
        FullName = auth.FullName is { Length: > 0 } name ? name : "—";
        Initials = string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        Subtitle = auth.Role;
        WarehouseName = warehouseContext.WarehouseName is { Length: > 0 } wh ? wh : "—";
        LanguageName = LangNames[Math.Max(0, Array.IndexOf(LangCodes, Loc.Instance.Language))];
        Footer = $"Cartex Do'kon {AppInfo.Current.VersionString} • {session.ServerUrl}";
        ThemeName = Loc.Instance["theme_" + Preferences.Get("app_theme", "system")];
    }

    [RelayCommand]
    private async Task ChangeWarehouseAsync()
    {
        await warehouseContext.ChangeAsync();
        Appear();
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
        var choice = await Shell.Current.CurrentPage.DisplayActionSheet(
            Loc.Instance["theme"], Loc.Instance["cancel"], null, names);
        var index = Array.IndexOf(names, choice);
        if (index < 0) return;
        Preferences.Set("app_theme", keys[index]);
        ApplyTheme();
        Appear();
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
        var choice = await Shell.Current.CurrentPage.DisplayActionSheet(
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
        if (!await page.DisplayAlert(Loc.Instance["logout"], Loc.Instance["logout_confirm"], Loc.Instance["logout"], Loc.Instance["cancel"]))
            return;
        AppLock.Disable();
        await auth.LogoutAsync();
        cart.Clear();
        warehouseContext.Reset();
        await Shell.Current.GoToAsync("//login");
    }
}
