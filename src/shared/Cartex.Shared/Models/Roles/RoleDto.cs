namespace Cartex.Shared.Models.Roles;

public record RoleDto(long Id, string Name, string? Description, List<string> Permissions);
