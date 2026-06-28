using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Role : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}
