namespace Cartex.Shared.Models.Auth;

public record LoginWithKeyRequest(string KeyContent, string Serial, string? DeviceName = null);

public record GenerateHardwareKeyRequest(long UserId, string Serial);

public record HardwareKeyResult(string FileName, string Content);

public record HardwareKeyDto(long Id, long UserId, string Username, string FullName, string Serial, DateTime IssuedAt, DateTime? RevokedAt);
