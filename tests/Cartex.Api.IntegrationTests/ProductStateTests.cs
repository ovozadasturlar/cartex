using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ProductStateTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record ProductRow(long Id, long DefaultVariantId, string Name, bool IsEnabled);
    private sealed record StockRow(long VariantId, string ProductName, decimal Quantity);
    private sealed record StockPage(List<StockRow> Items, int TotalCount);
    private sealed record SalesPolicyRow(
        string ShiftPolicy,
        decimal? MaxDiscountPercent,
        decimal DefaultMinStock,
        int StaleRateDays,
        bool AllowDebtSales,
        bool AllowCustomerCredit,
        bool RequireDebtDueDate,
        bool RequireSupplier,
        bool ShowOutOfStock);

    private static Task<HttpResponseMessage> SetStateAsync(HttpClient client, long productId, bool isEnabled) =>
        client.PutAsJsonAsync($"/api/products/{productId}/state", new { isEnabled });

    private static Task<StockPage?> OnHandAsync(HttpClient client, long warehouseId, bool forSale) =>
        client.GetFromJsonAsync<StockPage>($"/api/stocks/on-hand?warehouseId={warehouseId}&page=1&pageSize=200&forSale={forSale.ToString().ToLowerInvariant()}");

    private static async Task<long> CreateProductAsync(HttpClient client, string name)
    {
        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name,
            categoryId = (long?)null,
            unitId = units![0].Id,
            minStock = 0m,
            barcodes = (List<string>?)null
        });
        create.EnsureSuccessStatusCode();
        return await create.Content.ReadFromJsonAsync<long>();
    }

    private static async Task<ProductRow> FindProductAsync(HttpClient client, long productId)
    {
        var products = await client.GetFromJsonAsync<List<ProductRow>>("/api/products");
        return products!.First(p => p.Id == productId);
    }

    [Fact]
    public async Task SetState_TogglesIsEnabled_ForAdmin()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var productId = await CreateProductAsync(admin, "State toggle product");

        Assert.True((await FindProductAsync(admin, productId)).IsEnabled);

        (await SetStateAsync(admin, productId, false)).EnsureSuccessStatusCode();
        Assert.False((await FindProductAsync(admin, productId)).IsEnabled);

        (await SetStateAsync(admin, productId, true)).EnsureSuccessStatusCode();
        Assert.True((await FindProductAsync(admin, productId)).IsEnabled);
    }

    [Fact]
    public async Task SetState_Is403_ForSeller()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var productId = await CreateProductAsync(admin, "State permission product");

        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var response = await SetStateAsync(seller, productId, false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True((await FindProductAsync(admin, productId)).IsEnabled);
    }

    [Fact]
    public async Task OnHand_ForSale_OmitsDisabledProduct()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var warehouses = await admin.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var warehouseId = warehouses![0].Id;

        var stocked = await OnHandAsync(admin, warehouseId, false);
        var target = stocked!.Items.First(i => i.Quantity > 0);

        var products = await admin.GetFromJsonAsync<List<ProductRow>>("/api/products");
        var productId = products!.First(p => p.DefaultVariantId == target.VariantId).Id;

        try
        {
            (await SetStateAsync(admin, productId, false)).EnsureSuccessStatusCode();

            var forSale = await OnHandAsync(admin, warehouseId, true);
            Assert.DoesNotContain(forSale!.Items, i => i.VariantId == target.VariantId);

            var all = await OnHandAsync(admin, warehouseId, false);
            Assert.Contains(all!.Items, i => i.VariantId == target.VariantId);
        }
        finally
        {
            (await SetStateAsync(admin, productId, true)).EnsureSuccessStatusCode();
        }

        var reenabled = await OnHandAsync(admin, warehouseId, true);
        Assert.Contains(reenabled!.Items, i => i.VariantId == target.VariantId);
    }

    [Fact]
    public async Task SalesPolicy_ShowOutOfStock_Roundtrips()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var original = await admin.GetFromJsonAsync<SalesPolicyRow>("/api/settings/sales-policy");

        try
        {
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", original! with { ShowOutOfStock = true })).EnsureSuccessStatusCode();
            var enabled = await admin.GetFromJsonAsync<SalesPolicyRow>("/api/settings/sales-policy");
            Assert.True(enabled!.ShowOutOfStock);

            (await admin.PutAsJsonAsync("/api/settings/sales-policy", original with { ShowOutOfStock = false })).EnsureSuccessStatusCode();
            var disabled = await admin.GetFromJsonAsync<SalesPolicyRow>("/api/settings/sales-policy");
            Assert.False(disabled!.ShowOutOfStock);
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/settings/sales-policy", original!)).EnsureSuccessStatusCode();
        }
    }
}
