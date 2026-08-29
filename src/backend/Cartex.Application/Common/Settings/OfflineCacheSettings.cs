namespace Cartex.Application.Common.Settings;

public sealed class OfflineCacheSettings
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public DateTime? ClaimedAt { get; set; }
}

/// HUB-04: do'kon tarmog'idagi guvohnomalarni imzolash uchun o'rnatma kaliti (ECDSA P-256, PKCS#8).
public sealed class HubAttestationKeySettings
{
    public string? PrivateKey { get; set; }
}
