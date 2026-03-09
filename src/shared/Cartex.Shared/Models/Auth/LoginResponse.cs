namespace Cartex.Shared.Models.Auth;

public record LoginResponse(string Token, string FullName, string Role);
