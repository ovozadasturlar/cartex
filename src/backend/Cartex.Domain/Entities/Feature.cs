using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Feature : AuditableEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
}
