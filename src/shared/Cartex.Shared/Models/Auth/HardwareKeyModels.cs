namespace Cartex.Shared.Models.Auth;

public record LoginWithKeyRequest(string KeyContent, string Serial, string? DeviceName = null);

public record GenerateHardwareKeyRequest(long UserId, string Serial);

public record HardwareKeyResult(string FileName, string Content);

public record SetHardwareKeyEnabledRequest(bool Enabled);

public record HardwareKeyDto(long Id, long UserId, string Username, string FullName, string Serial, DateTime IssuedAt, bool IsEnabled, DateTime? RevokedAt);
