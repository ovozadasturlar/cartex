using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class Role : SoftDeleteEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? StartPage { get; set; }
    public int Priority { get; set; }
    public int Level { get; set; }
    public bool IsSystem { get; set; }
    public bool AccessAll { get; set; }
    public List<string> GrantablePermissions { get; set; } = [];
    public List<string> AssignableRoles { get; set; } = [];

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
