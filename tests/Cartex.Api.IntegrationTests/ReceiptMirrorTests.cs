using System.Net.Http.Json;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class ReceiptMirrorTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);
    private sealed record SaleResult(long SaleId, string ReceiptToken);

    [Fact]
    public async Task Sale_Enqueues_ReceiptMirrorEvent()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");
        await AuthHelper.EnsureOpenShiftAsync(client);

        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=Sprite");
        var sprite = products!.First(p => p.Name == "Sprite 1L");

        var response = await client.PostAsJsonAsync("/api/sales", new
        {
            warehouseId = warehouses![0].Id,
            customerId = (long?)null,
            paidCash = 50_000m,
            paidCard = 0m,
            paidBonus = 0m,
            items = new[] { new { variantId = sprite.DefaultVariantId, quantity = 1m } },
            discountAmount = 0m
        });
        response.EnsureSuccessStatusCode();
        var sale = (await response.Content.ReadFromJsonAsync<SaleResult>())!;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.NotificationOutbox
            .Where(m => m.EventType.Contains("ReceiptMirrorEvent"))
            .ToListAsync();

        Assert.Contains(rows, r => r.Payload.Contains(sale.ReceiptToken));
    }
}
