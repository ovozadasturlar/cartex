namespace Cartex.Shared.Models.Auth;

public record QrLoginStartResponse(string Code);

public record ApproveQrLoginRequest(string Code);

public record PollQrLoginRequest(string Code, string? DeviceName = null);
