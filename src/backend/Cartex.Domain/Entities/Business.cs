using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Business : AuditableEntity
{
    public string Name { get; set; } = null!;
    public string? LegalName { get; set; }

    public ICollection<Branch> Branches { get; set; } = [];
}
