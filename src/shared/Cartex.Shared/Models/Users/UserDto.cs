namespace Cartex.Shared.Models.Users;

public record UserDto(long Id, string FullName, string Username, string RoleName, string ShopName, bool IsActive);
