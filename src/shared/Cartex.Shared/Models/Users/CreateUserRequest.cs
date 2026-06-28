namespace Cartex.Shared.Models.Users;

public record CreateUserRequest(string FullName, string Username, string Password, long RoleId, long? DefaultBranchId, List<long> BranchIds);
