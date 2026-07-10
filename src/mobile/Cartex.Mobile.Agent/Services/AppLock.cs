using System.Security.Cryptography;
using System.Text;

namespace Cartex.Mobile.Agent.Services;

public static class AppLock
{
    public static bool PinEnabled => Preferences.Get("pin_enabled", false);
    public static bool BiometricEnabled => Preferences.Get("bio_enabled", false);

    public static async Task SetPinAsync(string pin)
    {
        await SecureStorage.SetAsync("pin_hash", Hash(pin));
        Preferences.Set("pin_enabled", true);
    }

    public static void SetBiometric(bool enabled) => Preferences.Set("bio_enabled", enabled);

    public static int LockAfterSeconds
    {
        get => Preferences.Get("lock_after", 0);
        set => Preferences.Set("lock_after", value);
    }

    public static void Disable()
    {
        Preferences.Set("pin_enabled", false);
        Preferences.Set("bio_enabled", false);
        SecureStorage.Remove("pin_hash");
    }

    public static async Task<bool> VerifyAsync(string pin) =>
        await SecureStorage.GetAsync("pin_hash") == Hash(pin);

    private static string Hash(string pin) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("cartex:" + pin)));
}

public interface IBiometricAuth
{
    bool IsAvailable { get; }
    Task<bool> AuthenticateAsync(string title);
}
