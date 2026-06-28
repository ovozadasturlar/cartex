using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class User : SoftDeleteEntity
{
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public long? DefaultBranchId { get; set; }
    public Branch? DefaultBranch { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserBranch> UserBranches { get; set; } = [];
}
