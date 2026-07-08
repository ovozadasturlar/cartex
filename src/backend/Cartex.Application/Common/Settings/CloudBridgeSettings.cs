namespace Cartex.Application.Common.Settings;

public sealed class CloudBridgeSettings
{
    public bool Enabled { get; set; }
    public string? GatewayUrl { get; set; }
    public string? LicenseKey { get; set; }
}
