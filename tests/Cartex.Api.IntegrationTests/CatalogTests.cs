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

    private sealed record UnitRow(long Id, string Name, string ShortName);

    [Fact]
    public async Task PackBarcode_Lookup_ReturnsPackQty()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var units = await client.GetFromJsonAsync<List<UnitRow>>("/api/units");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var unitId = units!.First(u => u.ShortName == "dona").Id;

        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Pack lookup product",
            categoryId = (long?)null,
            unitId,
            minStock = 0m,
            barcodes = new[]
            {
                new { code = "8000000000101", packQty = 1m },
                new { code = "8000000000102", packQty = 6m }
            }
        });
        create.EnsureSuccessStatusCode();

        var unit = await client.GetFromJsonAsync<ProductLookupDto>($"/api/products/by-barcode?code=8000000000101&warehouseId={warehouses![0].Id}");
        var pack = await client.GetFromJsonAsync<ProductLookupDto>($"/api/products/by-barcode?code=8000000000102&warehouseId={warehouses[0].Id}");
        Assert.Equal(1m, unit!.PackQty);
        Assert.Equal(6m, pack!.PackQty);
    }

    [Fact]
    public async Task GenerateBarcode_UnitAndPack_ShareSerial_AndAreIdempotent()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var units = await client.GetFromJsonAsync<List<UnitRow>>("/api/units");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var unitId = units!.First(u => u.ShortName == "dona").Id;

        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Generate test product",
            categoryId = (long?)null,
            unitId,
            minStock = 0m,
            barcodes = new[] { new { code = "8000000000201", packQty = 1m } }
        });
        create.EnsureSuccessStatusCode();
        var productId = await create.Content.ReadFromJsonAsync<long>();
        var variants = await client.GetFromJsonAsync<List<VariantRow>>($"/api/products/{productId}/variants");
        var variantId = variants!.Single().Id;

        async Task<string> GenerateAsync(string query)
        {
            var response = await client.PostAsync($"/api/barcodes/generate/{variantId}{query}", null);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadAsStringAsync()).Trim('"');
        }

        var unitCode = await GenerateAsync("");
        var packCode = await GenerateAsync("?packQty=6");

        Assert.Equal($"CTX-{variantId:D6}", unitCode);
        Assert.Equal($"CTX-P6-{variantId:D6}", packCode);

        var again = await GenerateAsync("?packQty=6");
        Assert.Equal(packCode, again);

        var lookup = await client.GetFromJsonAsync<ProductLookupDto>($"/api/products/by-barcode?code={packCode}&warehouseId={warehouses![0].Id}");
        Assert.Equal(6m, lookup!.PackQty);

        var mutate = await client.PutAsJsonAsync($"/api/products/variants/{variantId}", new
        {
            name = (string?)null,
            code = (string?)null,
            attributes = (string?)null,
            imageKey = (string?)null,
            barcodes = new[]
            {
                new { code = "8000000000201", packQty = 1m },
                new { code = unitCode, packQty = 1m },
                new { code = packCode, packQty = 8m }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, mutate.StatusCode);

        var mismatchedCreate = await client.PostAsJsonAsync("/api/barcodes",
            new { variantId, code = $"CTX-P9-{variantId:D6}", packQty = 4m });
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedCreate.StatusCode);
    }

    private sealed record VariantRow(long Id, bool IsDefault);

    [Fact]
    public async Task UnitChange_ToDifferentDimension_Is400()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        var units = await client.GetFromJsonAsync<List<UnitRow>>("/api/units");
        var dona = units!.First(u => u.ShortName == "dona").Id;
        var kg = units!.First(u => u.ShortName == "kg").Id;

        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Dimension guard product",
            categoryId = (long?)null,
            unitId = dona,
            minStock = 0m,
            barcodes = (List<object>?)null
        });
        create.EnsureSuccessStatusCode();
        var productId = await create.Content.ReadFromJsonAsync<long>();

        var update = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            name = "Dimension guard product",
            categoryId = (long?)null,
            unitId = kg,
            minStock = 0m
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
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
