using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class OrderingLifecycleTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record CartRow(long Id, string AggregateCode, string Status);

    private static async Task<HttpClient> DeveloperAsync(CartexApiFactory factory)
    {
        var client = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        (await client.PutAsJsonAsync("/api/features/ordering", new { isEnabled = true })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<string> SubmitCartAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products");
        var mixer = products!.First(p => p.Name == "Smesitel oshxona Zegor");
        var resp = await client.PostAsJsonAsync("/api/ordering/carts", new
        {
            warehouseId = warehouses![0].Id,
            customerId = (long?)null,
            items = new[] { new { variantId = mixer.DefaultVariantId, quantity = 1m } }
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadAsStringAsync()).Trim('"');
    }

    [Fact]
    public async Task Status_transitions_follow_lifecycle_rules()
    {
        var client = await DeveloperAsync(factory);
        var code = await SubmitCartAsync(client);

        var invalid = await client.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Ready" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        (await client.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Confirmed" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Ready" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Cancelled" })).EnsureSuccessStatusCode();

        var reopen = await client.PutAsJsonAsync($"/api/ordering/carts/{code}/status", new { status = "Open" });
        Assert.Equal(HttpStatusCode.BadRequest, reopen.StatusCode);

        var cancelled = await client.GetFromJsonAsync<List<CartRow>>("/api/ordering/carts?status=Cancelled");
        Assert.Contains(cancelled!, c => c.AggregateCode == code);
    }

    [Fact]
    public async Task Features_enabled_endpoint_is_available_to_seller()
    {
        await DeveloperAsync(factory);
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var enabled = await seller.GetFromJsonAsync<List<string>>("/api/features/enabled");
        Assert.NotNull(enabled);
        Assert.Contains("ordering", enabled);
    }
}
