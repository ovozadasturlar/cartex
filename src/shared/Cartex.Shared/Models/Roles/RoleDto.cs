namespace Cartex.Shared.Models.Roles;

public record RoleDto(long Id, string Name, string? Description, string? StartPage, int Priority, bool AccessAll, List<string> Permissions, List<string> GrantablePermissions, string? CartDestination = null)
{
    public List<string> AssignableRoles { get; init; } = [];

    /// Tizim roli tahrirlanmaydi va o'chirilmaydi. Server buni baribir rad etadi, lekin
    /// klient bilmasa tugma faol turadi va foydalanuvchi xato bilan uriladi.
    public bool IsSystem { get; init; }
    public bool RequiresBranch { get; init; }
    public bool IsActive { get; init; } = true;
}
