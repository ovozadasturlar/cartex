using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cartex.Api.IntegrationTests;

public sealed class CatalogStub : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    private WebApplication _app = null!;
    private int _requests;

    public CatalogProductDto? Product { get; set; }

    public int Requests => Volatile.Read(ref _requests);

    public string Url { get; private set; } = string.Empty;

    public static async Task<CatalogStub> StartAsync()
    {
        var stub = new CatalogStub();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.MapGet("/", (HttpRequest request) =>
        {
            Interlocked.Increment(ref stub._requests);
            var product = stub.Product;
            var payload = product is not null && product.Barcode == request.Query["barcode"].ToString()
                ? JsonSerializer.Serialize(new { product }, Format)
                : "{}";
            return Results.Text(payload, "application/json");
        });

        await app.StartAsync();
        stub._app = app;
        stub.Url = app.Urls.First();
        return stub;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

public static class CatalogSettings
{
    public static async Task<ProductReferenceSettings?> ReadAsync(CartexApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference);
    }

    public static async Task WriteAsync(CartexApiFactory factory, CatalogSourceMode mode, string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>().SetAsync(
            SettingKeys.ProductReference,
            new ProductReferenceSettings { Mode = mode, EndpointBaseUrl = endpoint });
    }

    public static async Task RestoreAsync(CartexApiFactory factory, ProductReferenceSettings? previous)
    {
        using var scope = factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        if (previous is null)
            await settings.RemoveAsync(SettingKeys.ProductReference);
        else
            await settings.SetAsync(SettingKeys.ProductReference, previous);
    }
}
