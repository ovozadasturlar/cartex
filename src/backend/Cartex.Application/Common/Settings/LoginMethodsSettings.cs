namespace Cartex.Application.Common.Settings;

public sealed class LoginMethodsSettings
{
    public bool QrEnabled { get; set; }
    public int QrRefreshSeconds { get; set; } = 120;
    public bool KeyEnabled { get; set; } = true;
}
