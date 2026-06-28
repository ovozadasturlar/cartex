using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Permission : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
