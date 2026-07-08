using System.Net;
using System.Net.Http.Json;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Api.IntegrationTests;

[Collection("api")]
public class StockTransferTests(CartexApiFactory factory)
{
    private sealed record IdName(long Id, string Name);
    private sealed record Product(long Id, long DefaultVariantId, string Name);

    [Fact]
    public async Task Receive_WithInsufficientSourceStock_Fails()
    {
        var client = await AuthHelper.LoginAsync(factory, "admin", "admin123");

        var units = await client.GetFromJsonAsync<List<IdName>>("/api/units");
        var warehouses = await client.GetFromJsonAsync<List<IdName>>("/api/warehouses");
        var fromId = warehouses![0].Id;

        var createWarehouse = await client.PostAsJsonAsync("/api/warehouses", new { name = "Transfer QA ombori" });
        createWarehouse.EnsureSuccessStatusCode();
        var toId = await createWarehouse.Content.ReadFromJsonAsync<long>();

        var createProduct = await client.PostAsJsonAsync("/api/products", new
        {
            name = "Transfer Guard Product",
            categoryId = (long?)null,
            unitId = units![0].Id,
            minStock = 0m,
            barcodes = (List<string>?)null
        });
        createProduct.EnsureSuccessStatusCode();

        var products = await client.GetFromJsonAsync<List<Product>>("/api/products?search=Transfer Guard");
        var variantId = products!.First(p => p.Name == "Transfer Guard Product").DefaultVariantId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchId = await db.Warehouses.Where(w => w.Id == fromId).Select(w => w.BranchId).FirstAsync();
            db.Stocks.Add(new Stock { BranchId = branchId, WarehouseId = fromId, VariantId = variantId, Quantity = 5, PurchasePrice = 1000 });
            await db.SaveChangesAsync();
        }

        var createTransfer = await client.PostAsJsonAsync("/api/stock-transfers", new
        {
            fromWarehouseId = fromId,
            toWarehouseId = toId,
            variantId,
            quantity = 5m
        });
        createTransfer.EnsureSuccessStatusCode();
        var transferId = await createTransfer.Content.ReadFromJsonAsync<long>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Stocks.Where(s => s.WarehouseId == fromId && s.VariantId == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, 3));
        }

        var receive = await client.PutAsync($"/api/stock-transfers/{transferId}/receive", null);
        Assert.Equal(HttpStatusCode.BadRequest, receive.StatusCode);

        await using (var check = factory.Services.CreateAsyncScope())
        {
            var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var transfer = await db.StockTransfers.FirstAsync(t => t.Id == transferId);
            Assert.Equal(TransferStatus.Sent, transfer.Status);
            var targetQty = await db.Stocks.Where(s => s.WarehouseId == toId && s.VariantId == variantId).SumAsync(s => s.Quantity);
            Assert.Equal(0, targetQty);
        }
    }
}
