namespace Cartex.Shared.Models.Users;

public record UpdateUserRequest(string FullName, long RoleId, bool IsActive, string? NewPassword, long? DefaultBranchId, List<long> BranchIds);
