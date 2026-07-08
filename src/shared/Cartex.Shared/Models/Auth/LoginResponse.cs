namespace Cartex.Shared.Models.Auth;

public record LoginResponse(string Token, string RefreshToken, string FullName, string Role);
