using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Application.Catalog;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Catalog;

namespace Cartex.Infrastructure.Catalog;

public sealed class OnlineCatalogSource(IHttpClientFactory clients, ISettingsService settings) : ICatalogSource
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public CatalogSourceMode Mode => CatalogSourceMode.Online;

    public async Task<CatalogProductDto?> ByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        if (barcode.Length is < 8 or > 14 || !barcode.All(char.IsAsciiDigit))
            return null;

        var payload = await ReadAsync($"barcode={Uri.EscapeDataString(barcode)}", cancellationToken);
        return payload is not null
            && payload.Value.TryGetProperty("product", out var product)
            && product.ValueKind == JsonValueKind.Object
                ? Map(product)
                : null;
    }

    public async Task<IReadOnlyList<CatalogProductDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var route = $"q={Uri.EscapeDataString(query)}&limit={limit.ToString(CultureInfo.InvariantCulture)}";
        var payload = await ReadAsync(route, cancellationToken);
        if (payload is null || !payload.Value.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return [];

        return [.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(Map)];
    }

    private async Task<JsonElement?> ReadAsync(string route, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        var endpoint = config.EndpointBaseUrl.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url))
            return null;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);

        var separator = url.Query.Length > 0 ? '&' : '?';
        using var response = await clients.CreateClient().GetAsync(endpoint + separator + route, deadline.Token);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<JsonElement>(deadline.Token)
            : null;
    }

    private static CatalogProductDto Map(JsonElement product) => new(
        Text(product, "barcode") ?? string.Empty,
        Text(product, "name") ?? string.Empty,
        Text(product, "nameCyrl"),
        Text(product, "manufacturer"),
        Text(product, "categoryParent"),
        Text(product, "categoryChild"),
        Text(product, "model"),
        Text(product, "unit"),
        Number(product, "packQty"),
        Text(product, "imageUrl"));

    private static decimal? Number(JsonElement product, string name) =>
        product.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out var quantity)
            ? quantity
            : null;

    private static string? Text(JsonElement product, string name) =>
        product.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
