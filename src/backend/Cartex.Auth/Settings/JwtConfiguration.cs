using System.Text;
using Microsoft.Extensions.Configuration;

namespace Cartex.Auth.Settings;

public static class JwtConfiguration
{
    public static JwtSettings GetValidated(IConfiguration configuration)
    {
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw Missing("Jwt");
        Require(settings.Issuer, "Jwt:Issuer");
        Require(settings.Audience, "Jwt:Audience");
        Require(settings.CustomerAudience, "Jwt:CustomerAudience");
        if (string.IsNullOrWhiteSpace(settings.Key) || Encoding.UTF8.GetByteCount(settings.Key) < 32)
            throw new InvalidOperationException(
                "Jwt:Key is missing or shorter than 32 bytes. Set it via appsettings, user-secrets, or environment variables.");
        if (settings.ExpirationMinutes <= 0)
            throw new InvalidOperationException(
                "Jwt:ExpirationMinutes must be greater than zero. Set it via appsettings, user-secrets, or environment variables.");
        return settings;
    }

    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Missing(key);
    }

    private static InvalidOperationException Missing(string key) => new(
        $"{key} is missing. Set it via appsettings, user-secrets, or environment variables.");
}
