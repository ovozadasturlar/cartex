using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class PrepackTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record Label(long Id, string LabelCode, string ProductName, string UnitName, decimal Quantity, decimal Price);
    private sealed record Lookup(long PrepackId, long VariantId, string ProductName, string UnitName, string Dimension, decimal Quantity, decimal UnitPrice, decimal Price);

    [Fact]
    public async Task Prepack_Create_Scan_Sell_And_ReuseRejected()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);

        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=PPR");
        var pipe = products!.First(p => p.Name == "PPR truba 20mm PN20");

        var create = await client.PostAsJsonAsync("/api/prepacks",
            new { warehouseId, variantId = pipe.DefaultVariantId, quantity = 2.5m, count = 1 });
        create.EnsureSuccessStatusCode();
        var label = (await create.Content.ReadFromJsonAsync<List<Label>>())!.Single();
        Assert.StartsWith("PP", label.LabelCode);
        Assert.Equal(2.5m * 9500m, label.Price);

        var lookup = await client.GetFromJsonAsync<Lookup>($"/api/prepacks/by-code?code={label.LabelCode}&warehouseId={warehouseId}");
        Assert.Equal(2.5m, lookup!.Quantity);
        Assert.Equal(pipe.DefaultVariantId, lookup.VariantId);

        var sale = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = lookup.Price,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = lookup.VariantId, quantity = lookup.Quantity, prepackId = (long?)lookup.PrepackId } },
            discountAmount = 0m
        });
        sale.EnsureSuccessStatusCode();

        var rescan = await client.GetAsync($"/api/prepacks/by-code?code={label.LabelCode}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.BadRequest, rescan.StatusCode);

        var resell = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId,
            customerId = (long?)null,
            paidCash = lookup.Price,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = lookup.VariantId, quantity = lookup.Quantity, prepackId = (long?)lookup.PrepackId } },
            discountAmount = 0m
        });
        Assert.Equal(HttpStatusCode.BadRequest, resell.StatusCode);
    }

    [Fact]
    public async Task Prepack_FeatureDisabled_Is403_And_SellerAllowed()
    {
        var dev = await AuthHelper.LoginAsync(factory, "developer", "developer123");
        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var warehouses = await seller.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;
        var products = await seller.GetFromJsonAsync<List<Product>>("/api/products?search=PPR");
        var pipe = products!.First(p => p.Name == "PPR truba 25mm PN20");

        object body = new { warehouseId, variantId = pipe.DefaultVariantId, quantity = 1m, count = 1 };

        (await dev.PutAsJsonAsync("/api/features/prepack", new { isEnabled = false })).EnsureSuccessStatusCode();
        try
        {
            var blocked = await seller.PostAsJsonAsync("/api/prepacks", body);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        }
        finally
        {
            (await dev.PutAsJsonAsync("/api/features/prepack", new { isEnabled = true })).EnsureSuccessStatusCode();
            (await dev.PutAsJsonAsync("/api/features/modules/prepack", new { isEnabled = true })).EnsureSuccessStatusCode();
        }

        var allowed = await seller.PostAsJsonAsync("/api/prepacks", body);
        Assert.True(allowed.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Prepack_Cancel_RemovesFromActive()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=PPR");
        var pipe = products!.First(p => p.Name == "PPR truba 32mm PN20");

        var create = await client.PostAsJsonAsync("/api/prepacks",
            new { warehouseId, variantId = pipe.DefaultVariantId, quantity = 1m, count = 1 });
        create.EnsureSuccessStatusCode();
        var label = (await create.Content.ReadFromJsonAsync<List<Label>>())!.Single();

        var lookup = await client.GetFromJsonAsync<Lookup>($"/api/prepacks/by-code?code={label.LabelCode}&warehouseId={warehouseId}");

        var cancel = await client.DeleteAsync($"/api/prepacks/{lookup!.PrepackId}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var after = await client.GetAsync($"/api/prepacks/by-code?code={label.LabelCode}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }
}
