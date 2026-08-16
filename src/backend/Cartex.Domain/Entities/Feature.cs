using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Feature : AuditableEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    /// Whether the module is sold to this shop. The vendor's answer, tied to the tariff.
    public bool IsEnabled { get; set; } = true;

    /// Whether the shop wants to use it. The owner's answer (SOZ-08), kept in a separate column
    /// so a tariff change never silently overwrites what the owner chose, or the other way round.
    public bool OwnerEnabled { get; set; } = true;
}
