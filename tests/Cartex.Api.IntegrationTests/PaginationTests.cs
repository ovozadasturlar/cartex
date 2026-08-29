using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class PaginationTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Meta(int TotalCount, int Page, int PageSize, int TotalPages);
    private sealed record ProductRow(long Id, string Name);

    [Fact]
    public async Task Products_Paged_WritesMetadataHeader_AndRespectsPageSize()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        var unitId = units![0].Id;

        for (var i = 0; i < 5; i++)
        {
            var create = await client.PostAsJsonAsync("/api/products", new
            {
                name = $"Paged product {i}",
                categoryId = (long?)null,
                unitId,
                minStock = 0m,
                barcodes = (List<string>?)null
            });
            create.EnsureSuccessStatusCode();
        }

        var response = await client.GetAsync("/api/products?page=1&pageSize=2&sortBy=Name&descending=false");
        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.TryGetValues("X-Paging", out var values));
        var meta = JsonSerializer.Deserialize<Meta>(values.First(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(meta);
        Assert.Equal(2, meta.PageSize);
        Assert.True(meta.TotalCount >= 5);
        Assert.True(meta.TotalPages >= 3);

        var items = await response.Content.ReadFromJsonAsync<List<ProductRow>>();
        Assert.Equal(2, items!.Count);
    }

    [Fact]
    public async Task Products_UnpagedRequest_HasNoPagingHeader_ReturnsAll()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var response = await client.GetAsync("/api/products");
        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("X-Paging"));
    }
}
