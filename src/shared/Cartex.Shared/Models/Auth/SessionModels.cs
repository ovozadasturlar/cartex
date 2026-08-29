namespace Cartex.Shared.Models.Auth;

public record RefreshRequest(string RefreshToken, string? DeviceName = null, string? DeviceId = null);

public record LogoutRequest(string RefreshToken);

public record DeviceSessionDto(long Id, string? DeviceName, DateTime CreatedAt, DateTime LastUsedAt, DateTime ExpiresAt, string? Username = null, string? Client = null, bool IsOfflineHolder = false);
