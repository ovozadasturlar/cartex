using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class SecurityViewModel(IBiometricAuth biometric) : ObservableObject
{
    [ObservableProperty] private string _pinStatus = "";
    [ObservableProperty] private bool _bioEnabled;
    [ObservableProperty] private bool _bioAllowed;

    private bool _suppressBio;

    public void Appear()
    {
        PinStatus = Loc.Instance[AppLock.PinEnabled ? "enabled" : "disabled"];
        BioAllowed = AppLock.PinEnabled && biometric.IsAvailable;
        _suppressBio = true;
        BioEnabled = AppLock.BiometricEnabled;
        _suppressBio = false;
    }

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
            Appear();
        }
    }

    [RelayCommand]
    private Task ChangePasswordAsync() => Shell.Current.GoToAsync("change-password");
}
