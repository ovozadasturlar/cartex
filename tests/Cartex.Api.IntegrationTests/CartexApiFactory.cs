using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cartex.Api.IntegrationTests;

public sealed class CartexApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "CartexIntegrationTestSigningKey-CartexIntegrationTestSigningKey");
        Environment.SetEnvironmentVariable("Seed__DeveloperPassword", "developer123");
        Environment.SetEnvironmentVariable("Seed__Demo", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    public override async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("Jwt__Key", null);
        Environment.SetEnvironmentVariable("Seed__DeveloperPassword", null);
        Environment.SetEnvironmentVariable("Seed__Demo", null);
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<CartexApiFactory>;
