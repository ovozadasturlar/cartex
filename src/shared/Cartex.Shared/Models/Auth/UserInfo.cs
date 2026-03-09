namespace Cartex.Shared.Models.Auth;

public record UserInfo(long UserId, string Username, string FullName, string Role, List<string> Permissions);
