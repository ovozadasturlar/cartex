namespace Cartex.Shared.Models.Auth;

public record LoginRequest(string Username, string Password, string? DeviceName = null, string? DeviceId = null);
