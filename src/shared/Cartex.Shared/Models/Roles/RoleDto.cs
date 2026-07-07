namespace Cartex.Shared.Models.Roles;

public record RoleDto(long Id, string Name, string? Description, string? StartPage, int Priority, bool AccessAll, List<string> Permissions, List<string> GrantablePermissions)
{
    public List<string> AssignableRoles { get; init; } = [];
}
