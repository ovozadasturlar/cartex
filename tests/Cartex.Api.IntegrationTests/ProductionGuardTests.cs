using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ProductionGuardTests
{
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
