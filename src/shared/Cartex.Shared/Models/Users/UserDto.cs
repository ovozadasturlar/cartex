namespace Cartex.Shared.Models.Users;

public record UserDto(
    long Id, string FullName, string Username,
    List<long> RoleIds, List<string> RoleNames,
    long? DefaultBranchId, string? DefaultBranchName,
    List<long> BranchIds, string? StartPage, string? CartDestination, bool IsActive);
