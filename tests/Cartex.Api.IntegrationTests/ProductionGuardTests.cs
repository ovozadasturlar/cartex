using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ProductionGuardTests
{
    [Theory]
    [InlineData("Jwt__Issuer", "Jwt:Issuer")]
    [InlineData("Jwt__Audience", "Jwt:Audience")]
    [InlineData("Jwt__Key", "Jwt:Key")]
    [InlineData("ConnectionStrings__DefaultConnection", "ConnectionStrings:DefaultConnection")]
    public void WP20_missing_required_configuration_fails_to_start(string variable, string key)
    {
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "");
        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

            var ex = Record.Exception(() => factory.CreateClient());

            Assert.NotNull(ex);
            Assert.Contains(key, ex.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public void Production_without_developer_password_fails_to_start()
    {
        var previous = Environment.GetEnvironmentVariable("Seed__DeveloperPassword");
        Environment.SetEnvironmentVariable("Seed__DeveloperPassword", "");
        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var ex = Record.Exception(() => factory.CreateClient());
            Assert.NotNull(ex);
            Assert.Contains("DeveloperPassword", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Seed__DeveloperPassword", previous);
        }
    }
}
