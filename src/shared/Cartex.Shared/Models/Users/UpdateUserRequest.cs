namespace Cartex.Shared.Models.Users;

public record UpdateUserRequest(string FullName, List<long> RoleIds, bool IsActive, string? NewPassword, long? DefaultBranchId, List<long> BranchIds, string? StartPage, string? CartDestination = null);
