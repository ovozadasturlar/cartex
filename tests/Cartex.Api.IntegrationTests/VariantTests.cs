using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class VariantTests(CartexApiFactory factory)
{
    private sealed record UnitDto(long Id, string Name);
    private sealed record VariantBarcode(string Code, decimal PackQty);
    private sealed record VariantDto(long Id, long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, bool IsDefault, List<VariantBarcode> Barcodes);

    [Fact]
    public async Task Variant_FullLifecycle_Works()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var units = await client.GetFromJsonAsync<List<UnitDto>>("/api/units");
        var unitId = units![0].Id;

        var create = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Variant test product",
            categoryId = (long?)null,
            unitId,
            minStock = 0m,
            barcodes = (List<string>?)null
        });
        create.EnsureSuccessStatusCode();
        var productId = await create.Content.ReadFromJsonAsync<long>();

        var initial = await client.GetFromJsonAsync<List<VariantDto>>($"/api/products/{productId}/variants");
        Assert.Single(initial!);
        Assert.True(initial![0].IsDefault);

        var addVariant = await client.PostAsJsonAsync($"/api/products/{productId}/variants", new
        {
            name = "Large",
            code = "VT-L",
            attributes = (string?)null,
            imageKey = (string?)null,
            barcodes = new[] { new { code = "7000000000001", packQty = 6m } }
        });
        addVariant.EnsureSuccessStatusCode();
        var variantId = await addVariant.Content.ReadFromJsonAsync<long>();

        var afterAdd = await client.GetFromJsonAsync<List<VariantDto>>($"/api/products/{productId}/variants");
        Assert.Equal(2, afterAdd!.Count);
        var added = afterAdd.Single(v => v.Id == variantId);
        Assert.Equal("Large", added.Name);
        Assert.Contains(added.Barcodes, b => b.Code == "7000000000001" && b.PackQty == 6m);

        var update = await client.PutAsJsonAsync($"/api/products/variants/{variantId}", new
        {
            name = "Extra Large",
            code = "VT-XL",
            attributes = (string?)null,
            imageKey = (string?)null,
            barcodes = new[] { new { code = "7000000000002", packQty = 1m } }
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var afterUpdate = await client.GetFromJsonAsync<List<VariantDto>>($"/api/products/{productId}/variants");
        var updated = afterUpdate!.Single(v => v.Id == variantId);
        Assert.Equal("Extra Large", updated.Name);
        Assert.Contains(updated.Barcodes, b => b.Code == "7000000000002");
        Assert.DoesNotContain(updated.Barcodes, b => b.Code == "7000000000001");

        var defaultId = afterUpdate!.Single(v => v.IsDefault).Id;
        var deleteDefault = await client.DeleteAsync($"/api/products/variants/{defaultId}");
        Assert.False(deleteDefault.IsSuccessStatusCode);

        var deleteVariant = await client.DeleteAsync($"/api/products/variants/{variantId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteVariant.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<List<VariantDto>>($"/api/products/{productId}/variants");
        Assert.Single(afterDelete!);
    }
}
