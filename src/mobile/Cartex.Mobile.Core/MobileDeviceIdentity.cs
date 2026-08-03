namespace Cartex.Mobile.Core;

public static class MobileDeviceIdentity
{
    public static string DeviceId
    {
        get
        {
            var value = Preferences.Get("device_id", "");
            if (!string.IsNullOrWhiteSpace(value)) return value;
            value = Guid.NewGuid().ToString("N");
            Preferences.Set("device_id", value);
            return value;
        }
    }

    public static string DeviceName => DeviceInfo.Current.Name is { Length: > 0 } name
        ? name
        : DeviceInfo.Current.Model;
}

