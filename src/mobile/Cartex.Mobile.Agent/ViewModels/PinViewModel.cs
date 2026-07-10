using Cartex.Mobile.Agent.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class PinViewModel(IBiometricAuth biometric) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private int _filled;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _showBiometric;

    private bool _setup;
    private string _entry = "";
    private string? _firstEntry;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _setup = query.TryGetValue("setup", out var v) && v?.ToString() == "1";
        Title = Loc.Instance[_setup ? "pin_set_title" : "pin_enter_title"];
        ShowBiometric = !_setup && AppLock.BiometricEnabled && biometric.IsAvailable;
    }

    public async Task AppearAsync()
    {
        if (ShowBiometric)
            await BiometricCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task DigitAsync(string digit)
    {
        if (_entry.Length >= 4) return;
        _entry += digit;
        Filled = _entry.Length;
        Error = null;
        if (_entry.Length == 4)
            await CompleteAsync();
    }

    [RelayCommand]
    private void Backspace()
    {
        if (_entry.Length == 0) return;
        _entry = _entry[..^1];
        Filled = _entry.Length;
    }

    [RelayCommand]
    private async Task BiometricAsync()
    {
        if (await biometric.AuthenticateAsync(Loc.Instance["biometric_title"]))
            await UnlockAsync();
    }

    private async Task CompleteAsync()
    {
        var pin = _entry;
        _entry = "";

        if (_setup)
        {
            if (_firstEntry is null)
            {
                _firstEntry = pin;
                Filled = 0;
                Title = Loc.Instance["pin_repeat_title"];
                return;
            }
            if (_firstEntry != pin)
            {
                _firstEntry = null;
                Filled = 0;
                Title = Loc.Instance["pin_set_title"];
                Error = Loc.Instance["pin_mismatch"];
                return;
            }
            await AppLock.SetPinAsync(pin);
            Ui.Toast(Loc.Instance["pin_enabled_toast"]);
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (await AppLock.VerifyAsync(pin))
        {
            await UnlockAsync();
            return;
        }
        Filled = 0;
        Error = Loc.Instance["pin_wrong"];
    }

    private static async Task UnlockAsync()
    {
        await Shell.Current.GoToAsync("//home");
    }
}
