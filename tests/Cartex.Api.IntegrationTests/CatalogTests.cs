using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class CatalogTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record ProductLookupDto(long VariantId, string ProductName, string UnitName, decimal PackQty, decimal SellingPrice, decimal OnHand);

    [Fact]
    public async Task PosLookup_FindsProduct_ByCustomCode()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var unitId = units![0].Id;
        var warehouseId = warehouses![0].Id;

        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Code lookup product",
            categoryId = (long?)null,
            unitId,
            minStock = 0m,
            barcodes = (List<string>?)null,
            code = "LOOKUP-SKU-9"
        });
        create.EnsureSuccessStatusCode();

        var lookup = await client.GetFromJsonAsync<ProductLookupDto>($"/api/products/by-barcode?code=LOOKUP-SKU-9&warehouseId={warehouseId}");
        Assert.NotNull(lookup);
        Assert.Equal("Code lookup product", lookup!.ProductName);
    }

    [Fact]
    public async Task InlineCreate_Is403_ForSeller_But200_ForAdmin()
    {
        var admin = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var units = await admin.GetFromJsonAsync<List<IdName>>("/api/units");
        var unitId = units![0].Id;

        object body = new { name = "Perm test", categoryId = (long?)null, unitId, minStock = 0m, barcodes = (List<string>?)null };

        var seller = await AuthHelper.LoginAsync(factory, "seller", "seller123");
        var sellerResponse = await seller.PostAsJsonAsync("/api/products", body);
        Assert.Equal(HttpStatusCode.Forbidden, sellerResponse.StatusCode);

        var adminResponse = await admin.PostAsJsonAsync("/api/products", body);
        Assert.True(adminResponse.IsSuccessStatusCode);
    }
}
