namespace Cartex.Shared.Models.Permissions;

public record PermissionDto(long Id, string Name, string? Description, bool IsEnabled)
{
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}
