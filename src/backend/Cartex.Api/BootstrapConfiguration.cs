using Cartex.Auth.Settings;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cartex.Api;

public static class BootstrapConfiguration
{
    public static string Validate(IConfiguration configuration, bool isDevelopment)
    {
        JwtConfiguration.GetValidated(configuration);
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw Missing("ConnectionStrings:DefaultConnection");
        if (!isDevelopment)
        {
            Require(configuration["Seed:DeveloperPassword"], "Seed:DeveloperPassword");
            Require(configuration["Seed:AdminPassword"], "Seed:AdminPassword");
        }
        return connectionString;
    }

    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Missing(key);
    }

    private static InvalidOperationException Missing(string key) => new(
        $"{key} is missing. Set it via appsettings, user-secrets, or environment variables.");
}

public sealed class BootstrapConfigurationHealthCheck(
    IConfiguration configuration,
    IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            BootstrapConfiguration.Validate(configuration, environment.IsDevelopment());
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(exception.Message, exception));
        }
    }
}
