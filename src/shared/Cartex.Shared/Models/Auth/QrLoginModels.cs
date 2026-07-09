namespace Cartex.Shared.Models.Auth;

public record QrLoginStartResponse(string Code, int ExpiresInSeconds);

public record ApproveQrLoginRequest(string Code);

public record PollQrLoginRequest(string Code, string? DeviceName = null);
