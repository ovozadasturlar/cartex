namespace Cartex.Shared.Models.Auth;

public record LoginWithKeyRequest(string KeyContent, string Serial);

public record GenerateHardwareKeyRequest(long UserId, string Serial);

public record HardwareKeyResult(string FileName, string Content);
