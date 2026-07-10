namespace Cartex.Shared.Models.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
