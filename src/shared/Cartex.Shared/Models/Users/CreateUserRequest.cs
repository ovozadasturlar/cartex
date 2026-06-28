namespace Cartex.Shared.Models.Users;

public record CreateUserRequest(string FullName, string Username, string Password, List<long> RoleIds, long? DefaultBranchId, List<long> BranchIds, string? StartPage);
