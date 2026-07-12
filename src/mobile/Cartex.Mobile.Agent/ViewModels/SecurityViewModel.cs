using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class SecurityViewModel(IBiometricAuth biometric) : ObservableObject
{
    [ObservableProperty] private string _pinStatus = "";
    [ObservableProperty] private string _lockAfterName = "";
    [ObservableProperty] private bool _pinOn;
    [ObservableProperty] private bool _bioEnabled;
    [ObservableProperty] private bool _bioAllowed;

    private bool _suppressBio;

    public void Appear()
    {
        PinStatus = Loc.Instance[AppLock.PinEnabled ? "enabled" : "disabled"];
        PinOn = AppLock.PinEnabled;
        LockAfterName = LockName(AppLock.LockAfterSeconds);
        BioAllowed = AppLock.PinEnabled && biometric.IsAvailable;
        _suppressBio = true;
        BioEnabled = AppLock.BiometricEnabled;
        _suppressBio = false;
    }

    partial void OnBioEnabledChanged(bool value)
    {
        if (_suppressBio) return;
        if (!value)
        {
            AppLock.SetBiometric(false);
            return;
        }
        _ = ConfirmBiometricAsync();
    }

    private async Task ConfirmBiometricAsync()
    {
        var ok = AppLock.PinEnabled && biometric.IsAvailable
            && await biometric.AuthenticateAsync(Loc.Instance["biometric_title"]);
        if (ok)
        {
            AppLock.SetBiometric(true);
            return;
        }
        _suppressBio = true;
        BioEnabled = false;
        _suppressBio = false;
        if (!AppLock.PinEnabled || !biometric.IsAvailable)
            Ui.Toast(Loc.Instance["biometric_unavailable"]);
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
            Appear();
        }
    }

    private static readonly int[] LockOptions = [0, 30, 60, 300];

    private static string LockName(int seconds) => seconds switch
    {
        0 => Loc.Instance["lock_now"],
        30 => "30 " + Loc.Instance["seconds"],
        60 => "1 " + Loc.Instance["minute"],
        _ => "5 " + Loc.Instance["minute"]
    };

    [RelayCommand]
    private async Task ChooseLockAfterAsync()
    {
        var names = LockOptions.Select(LockName).ToArray();
        var choice = await Shell.Current.CurrentPage.DisplayActionSheet(
            Loc.Instance["lock_after"], Loc.Instance["cancel"], null, names);
        var index = Array.IndexOf(names, choice);
        if (index < 0) return;
        AppLock.LockAfterSeconds = LockOptions[index];
        Appear();
    }

    [RelayCommand]
    private Task ChangePasswordAsync() => Shell.Current.GoToAsync("change-password");
}
