using System.Net.Http.Json;
using System.Text.Json;
using Cartex.Application.Common.Interfaces;

namespace Cartex.Infrastructure.Catalog;

public sealed class OpenFoodFactsProvider(IHttpClientFactory httpClientFactory) : IProductCatalogProvider
{
    public async Task<ProductCatalogInfo?> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var url = $"https://world.openfoodfacts.org/api/v2/product/{Uri.EscapeDataString(barcode)}.json?fields=product_name,brands,image_url";

        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (!doc.TryGetProperty("status", out var status) || status.GetInt32() != 1)
                return null;

            var product = doc.GetProperty("product");
            return new ProductCatalogInfo(
                Read(product, "product_name"),
                Read(product, "brands"),
                Read(product, "image_url"));
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string? Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
