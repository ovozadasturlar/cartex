namespace Cartex.Shared.Models.Roles;

public record UpdateRoleRequest(string Name, string? Description, string? StartPage, int Priority, List<string>? GrantablePermissions = null, List<string>? AssignableRoles = null);
