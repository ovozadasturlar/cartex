namespace Cartex.Application.Common.Settings;

public sealed class SmsSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "eskiz";
    public string? Login { get; set; }
    public string? Password { get; set; }
    public string? Sender { get; set; }
    public string? BaseUrl { get; set; }
}
