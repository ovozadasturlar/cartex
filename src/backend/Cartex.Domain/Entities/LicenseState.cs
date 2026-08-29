using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class LicenseState : AuditableEntity
{
    public const long SingletonId = 1;

    public string Tariff { get; set; } = "free";
    public DateTime? ExpiresAt { get; set; }
    public string? EnabledFeatures { get; set; }
    public string? Signature { get; set; }
}
