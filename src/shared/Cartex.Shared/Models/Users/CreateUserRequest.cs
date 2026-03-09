namespace Cartex.Shared.Models.Users;

public record CreateUserRequest(long ShopId, string FullName, string Username, string Password, long RoleId);
