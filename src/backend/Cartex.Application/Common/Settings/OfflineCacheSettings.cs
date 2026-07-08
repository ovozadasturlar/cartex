namespace Cartex.Application.Common.Settings;

public sealed class OfflineCacheSettings
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public DateTime? ClaimedAt { get; set; }
}
