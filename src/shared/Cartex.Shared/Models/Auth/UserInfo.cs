namespace Cartex.Shared.Models.Auth;

public record UserInfo(long UserId, string Username, string FullName, List<string> Roles, string? StartPage, List<string> Permissions);
