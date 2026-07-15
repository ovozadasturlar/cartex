namespace Cartex.Shared.Models.Roles;

public record CreateRoleRequest(string Name, string? Description, string? StartPage, int Priority, List<string>? GrantablePermissions = null, List<string>? AssignableRoles = null, string? CartDestination = null);
