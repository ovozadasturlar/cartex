using System.Net.Http.Json;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class DiscountRuleTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record SaleResult(long SaleId, string ReceiptToken);
    private sealed record Preview(decimal Total, List<PreviewLine> Applied);
    private sealed record PreviewLine(string Name, decimal Amount);

    [Fact]
    public async Task Sale_Applies_AutoDiscount_From_Rule()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);

        var create = await client.PostAsJsonAsync("/api/loyalty/discounts", new
        {
            id = 0L,
            name = "Test 10%",
            isEnabled = true,
            scope = "All",
            targetId = (long?)null,
            customerId = (long?)null,
            minAmount = 0m,
            method = "Percent",
            value = 10m,
            priority = 1
        });
        create.EnsureSuccessStatusCode();
        var ruleId = await create.Content.ReadFromJsonAsync<long>();

        try
        {
            var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
            var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=Sprite");
            var sprite = products!.First(p => p.Name == "Sprite 1L");

            var preview = await client.PostAsJsonAsync("/api/loyalty/discount-preview", new
            {
                customerId = (long?)null,
                items = new[] { new { variantId = sprite.DefaultVariantId, quantity = 2m, unitPrice = 10_000m } }
            });
            preview.EnsureSuccessStatusCode();
            var previewResult = (await preview.Content.ReadFromJsonAsync<Preview>())!;
            Assert.Equal(2_000m, previewResult.Total);

            var sale = await client.PostAsJsonAsync("/api/sales", new
            {
                warehouseId = warehouses![0].Id,
                customerId = (long?)null,
                paidCash = 500_000m,
                paidCard = 0m,
                paidBonus = 0m,
                items = new[] { new { variantId = sprite.DefaultVariantId, quantity = 1m } },
                discountAmount = 0m
            });
            sale.EnsureSuccessStatusCode();
            var result = (await sale.Content.ReadFromJsonAsync<SaleResult>())!;

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Sales.FirstAsync(s => s.Id == result.SaleId);
            var gross = row.TotalAmount + row.DiscountAmount;
            Assert.True(row.DiscountAmount > 0);
            Assert.Equal(Math.Round(gross * 0.10m, 2), row.DiscountAmount);
        }
        finally
        {
            (await client.DeleteAsync($"/api/loyalty/discounts/{ruleId}")).EnsureSuccessStatusCode();
        }
    }
}
