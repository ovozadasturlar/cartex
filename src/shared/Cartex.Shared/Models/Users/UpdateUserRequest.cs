namespace Cartex.Shared.Models.Users;

public record UpdateUserRequest(string FullName, long ShopId, long RoleId, bool IsActive, string? NewPassword);
