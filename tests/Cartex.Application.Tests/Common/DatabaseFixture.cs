using Cartex.Domain.Common;
using Cartex.Persistence;
using Cartex.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace Cartex.Application.Tests.Common;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private ServiceProvider _services = null!;
    private Respawner _respawner = null!;

    public TestCurrentUser CurrentUser { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddScoped<IFeatureStateProvider, TestFeatureStates>();
        services.AddScoped<Cartex.Application.Common.Interfaces.ISettingsService, TestSettingsService>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.IPagingMetadataWriter, NoopPagingWriter>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.ICartNotifier, NullCartNotifier>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.ISpreadsheetService, Cartex.Infrastructure.Import.ClosedXmlSpreadsheetService>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.IRemoteImageFetcher, NullRemoteImageFetcher>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.IImageProcessor, NullImageProcessor>();
        services.AddSingleton<Cartex.Application.Common.Interfaces.IObjectStorage, MemoryObjectStorage>();
        services.AddScoped<Cartex.Auth.Services.IPasswordHasher, Cartex.Auth.Services.PasswordHasher>();
        services.AddPersistence(_container.GetConnectionString());
        services.AddApplication();
        _services = services.BuildServiceProvider();

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
        }

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });

        await ResetAsync();
    }

    public async ValueTask ResetAsync()
    {
        CurrentUser.Reset();

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await DatabaseSeeder.SeedAsync(db, p => p);
        await DatabaseSeeder.SyncCurrenciesAsync(db);
        await DemoDataSeeder.SeedCatalogAsync(db);
    }

    public IServiceScope CreateScope() => _services.CreateScope();

    private sealed class NoopPagingWriter : Cartex.Application.Common.Interfaces.IPagingMetadataWriter
    {
        public void Write(Cartex.Application.Common.Models.PagedListMetadata metadata) { }
    }

    private sealed class NullCartNotifier : Cartex.Application.Common.Interfaces.ICartNotifier
    {
        public Task CartsChangedAsync(string kind, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullRemoteImageFetcher : Cartex.Application.Common.Interfaces.IRemoteImageFetcher
    {
        public Task<Cartex.Application.Common.Interfaces.RemoteImage?> FetchAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult<Cartex.Application.Common.Interfaces.RemoteImage?>(null);
    }

    private sealed class NullImageProcessor : Cartex.Application.Common.Interfaces.IImageProcessor
    {
        public Cartex.Application.Common.Interfaces.ProcessedImage? Process(Stream original) => null;
        public Cartex.Application.Common.Interfaces.ProcessedImage? ProcessMonochrome(Stream original) => null;
    }

    private sealed class MemoryObjectStorage : Cartex.Application.Common.Interfaces.IObjectStorage
    {
        public Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null) =>
            Task.FromResult(key ?? Guid.NewGuid().ToString("N"));

        public Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<(Stream, string)?>(null);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
