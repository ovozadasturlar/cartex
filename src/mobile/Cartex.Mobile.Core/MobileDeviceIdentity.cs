namespace Cartex.Mobile.Core;

public static class MobileDeviceIdentity
{
    private static string? _stableId;

    /// Derived from the OS device identity where possible so the phone keeps its trust
    /// after the app is reinstalled; a stored GUID is only the fallback.
    public static string DeviceId
    {
        get
        {
            if (_stableId is not null) return _stableId;
#if ANDROID
            try
            {
                var androidId = Android.Provider.Settings.Secure.GetString(
                    Android.App.Application.Context.ContentResolver,
                    Android.Provider.Settings.Secure.AndroidId);
                if (!string.IsNullOrWhiteSpace(androidId))
                    return _stableId = $"android-{androidId.ToLowerInvariant()}";
            }
            catch { }
#endif
            var value = Preferences.Get("device_id", "");
            if (!string.IsNullOrWhiteSpace(value)) return _stableId = value;
            value = Guid.NewGuid().ToString("N");
            Preferences.Set("device_id", value);
            return _stableId = value;
        }
    }

    private static string? _name;

    public static string DeviceName => _name ??= DeviceInfo.Current.Name is { Length: > 0 } name
        ? name
        : DeviceInfo.Current.Model;
}

