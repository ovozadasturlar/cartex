using Cartex.Domain.Common;

namespace Cartex.Domain.Entities;

public class User : SoftDeleteEntity
{
    public long ShopId { get; set; }
    public Shop Shop { get; set; } = null!;

    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
