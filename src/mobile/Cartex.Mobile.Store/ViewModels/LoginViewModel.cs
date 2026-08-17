using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.ViewModels;

public partial class LoginViewModel(
    MobileAuthService auth,
    SessionStore session,
    MobileOfflineService offline) : ObservableObject
{
    [ObservableProperty] private string _serverUrl = session.ServerUrl;
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isServerVisible;
    [ObservableProperty] private bool _hidePassword = true;
    [ObservableProperty] private string _eyeGlyph = "\U000F0208";
    [ObservableProperty] private string _languageShort = "";
    [ObservableProperty] private string? _error;

    private static readonly string[] LangCodes = ["uz-latn", "uz-cyrl", "ru", "en"];
    private static readonly string[] LangShorts = ["O'z", "Ўз", "Ру", "En"];
    private static readonly string[] LangNames = ["O'zbekcha (lotin)", "Ўзбекча (кирилл)", "Русский", "English"];

    public string ServerText => $"{Loc.Instance["server"]}: {ServerUrl}";

    partial void OnServerUrlChanged(string value) => OnPropertyChanged(nameof(ServerText));

    [RelayCommand]
    private void ToggleServer() => IsServerVisible = !IsServerVisible;

    [RelayCommand]
    private void ToggleEye()
    {
        HidePassword = !HidePassword;
        EyeGlyph = HidePassword ? "\U000F0208" : "\U000F0209";
    }

    [RelayCommand]
    private async Task ChooseLanguageAsync()
    {
        var choice = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            Loc.Instance["language"], Loc.Instance["cancel"], null, LangNames);
        var index = Array.IndexOf(LangNames, choice);
        if (index < 0) return;
        await SetLanguageAsync(LangCodes[index]);
    }

    private async Task SetLanguageAsync(string code)
    {
        await Loc.Instance.SetLanguageAsync(code);
        Error = null;
        LanguageShort = LangShorts[Math.Max(0, Array.IndexOf(LangCodes, code))];
        OnPropertyChanged(nameof(ServerText));
    }

    public void Initialize() =>
        LanguageShort = LangShorts[Math.Max(0, Array.IndexOf(LangCodes, Loc.Instance.Language))];

    private static async Task OfferPinSetupAsync()
    {
        if (AppLock.PinEnabled) return;
        await Task.Delay(600);
        try
        {
            if (Shell.Current?.CurrentPage is not { } page) return;
            if (await page.DisplayAlertAsync(Loc.Instance["pin_offer_title"], Loc.Instance["pin_offer_msg"],
                    Loc.Instance["pin_offer_yes"], Loc.Instance["later"]))
                await Shell.Current.GoToAsync("pin?setup=1");
        }
        catch { }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        Error = null;
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            Error = Loc.Instance["err_fill_all"];
            return;
        }
        if (!Uri.TryCreate(ServerUrl.Trim(), UriKind.Absolute, out _))
        {
            Error = Loc.Instance["err_bad_server"];
            return;
        }
        IsBusy = true;
        try
        {
            session.ServerUrl = ServerUrl.Trim();
            await auth.LoginAsync(Username.Trim(), Password);
            await offline.StartAsync();
            Password = "";
            await Shell.Current.GoToAsync("//main");
            _ = OfferPinSetupAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Error = Loc.Instance["err_bad_credentials"];
        }
        catch
        {
            Error = Loc.Instance["err_no_connection"];
            IsServerVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
