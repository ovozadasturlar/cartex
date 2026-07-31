namespace Cartex.Shared.Models.Permissions;

public record PermissionBundleDto(
    string Key,
    string Description,
    List<string> Permissions);
