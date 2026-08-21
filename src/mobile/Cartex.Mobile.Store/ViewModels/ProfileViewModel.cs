using Cartex.Mobile.Store.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ProfileViewModel(
    MobileAuthService auth,
    SessionStore session,
    StoreSignOut signOut,
    WarehouseContext warehouseContext,
    MobileOfflineService offline,
    MobileFeaturesCache features,
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
    [ObservableProperty] private bool _canViewDevices;
    [ObservableProperty] private string _offlineStatus = "";

    private static readonly string[] LangNames = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];
    private static readonly string[] LangCodes = ["uz-latn", "uz-cyrl", "ru", "en"];

    public async Task AppearAsync()
    {
        FullName = auth.FullName is { Length: > 0 } name ? name : "—";
        Initials = string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        Subtitle = auth.Role;
        WarehouseName = warehouseContext.WarehouseName is { Length: > 0 } wh ? wh : "—";
        LanguageName = LangNames[Math.Max(0, Array.IndexOf(LangCodes, Loc.Instance.Language))];
        Footer = $"Cartex Do'kon {AppInfo.Current.VersionString} • {session.ServerUrl}";
        ThemeName = Loc.Instance["theme_" + Preferences.Get("app_theme", "system")];
        CanViewDevices = permissions.Has("devices.view");
        // RUXSAT-04: oflayn kassa — alohida modul. To'liq huquqli foydalanuvchida ruxsatlar
        // tokendan olib tashlanmaydi, shuning uchun modul holati alohida so'raladi.
        await features.EnsureLoadedAsync();
        OfflineVisible = permissions.Has("devices.revoke")
            && permissions.HasAny("sales.create", "sales.checkout")
            && features.OfflineCacheEnabled;
        if (!OfflineVisible) return;
        // Vakolat kaliti saqlangan joydan o'qilmaguncha IsEnabled "yo'q" deydi, shuning
        // uchun holat faqat servis tayyor bo'lgach ko'rsatiladi.
        await offline.StartAsync();
        OfflineStatus = offline.IsEnabled ? WarehouseName : Loc.Instance["offline_mode_off"];
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
    private Task OpenOfflineSettingsAsync() => Shell.Current.GoToAsync("offline-settings");

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
        // Chiqish guvohnomani o'chiradi: yuborilmagan qatorlar keyingi kirishgacha HUB'ga
        // ketolmaydi. Vakolat kaliti o'qilmaguncha sanoq nol chiqadi, shuning uchun avval
        // xizmat ishga tushiriladi (server almashtirish yo'lidagi qo'riqchi bilan bir xil).
        await offline.StartAsync();
        var unsent = await offline.PendingCountAsync() + await offline.ErrorCountAsync();
        if (unsent > 0 && !await page.DisplayAlertAsync(
                Loc.Instance["logout"], string.Format(Loc.Instance["logout_pending_fmt"], unsent),
                Loc.Instance["logout"], Loc.Instance["cancel"]))
            return;
        await signOut.RunAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
