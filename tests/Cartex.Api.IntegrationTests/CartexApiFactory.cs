using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cartex.Api.IntegrationTests;

public sealed class CartexApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly string _catalogPath = Path.Combine(Path.GetTempPath(), "cartex-tests", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        Directory.CreateDirectory(_catalogPath);
        Environment.SetEnvironmentVariable("Catalog__PackPath", _catalogPath);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "CartexIntegrationTestSigningKey-CartexIntegrationTestSigningKey");
        Environment.SetEnvironmentVariable("Seed__DeveloperPassword", "developer123");
        Environment.SetEnvironmentVariable("Seed__Demo", "false");
        Environment.SetEnvironmentVariable("Seed__Catalog", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    public override async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("Jwt__Key", null);
        Environment.SetEnvironmentVariable("Seed__DeveloperPassword", null);
        Environment.SetEnvironmentVariable("Seed__Demo", null);
        Environment.SetEnvironmentVariable("Seed__Catalog", null);
        Environment.SetEnvironmentVariable("Catalog__PackPath", null);
        if (Directory.Exists(_catalogPath)) Directory.Delete(_catalogPath, recursive: true);
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<CartexApiFactory>;
