using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Business : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? LegalName { get; set; }
    public string Currency { get; set; } = "UZS";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? LogoImageKey { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
}
