using Cartex.Application.Stocks.Commands;
using Cartex.Application.StockTransfers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class StockOperationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long warehouse2, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var warehouse2 = (await db.Warehouses.Where(w => w.Id != warehouse1).Select(w => w.Id).FirstAsync());
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, warehouse2, businessId, adminId, variantId);
    }

    private async Task<decimal> StockSumAsync(long warehouse, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(s => s.WarehouseId == warehouse && s.VariantId == variantId).SumAsync(s => s.Quantity);
    }

    [Fact]
    public async Task Adjust_up_increases_stock_and_records_adjustment()
    {
        var (branch1, warehouse1, _, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var system = await StockSumAsync(warehouse1, variantId);
        var counted = system + 5;

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AdjustStockCommand(warehouse1, variantId, counted, "inventarizatsiya"));
        }

        Assert.Equal(counted, await StockSumAsync(warehouse1, variantId));

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adjustment = await db.StockAdjustments.FirstAsync(a => a.WarehouseId == warehouse1 && a.VariantId == variantId);
        Assert.Equal(5m, adjustment.Difference);
        Assert.Equal(system, adjustment.SystemQuantity);
        Assert.Equal(counted, adjustment.CountedQuantity);
    }

    [Fact]
    public async Task Adjust_down_decreases_stock()
    {
        var (branch1, warehouse1, _, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var system = await StockSumAsync(warehouse1, variantId);
        var counted = system - 3;

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AdjustStockCommand(warehouse1, variantId, counted, null));
        }

        Assert.Equal(counted, await StockSumAsync(warehouse1, variantId));
    }

    [Fact]
    public async Task Transfer_moves_stock_on_receive_and_conserves_total()
    {
        var (branch1, warehouse1, warehouse2, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var sourceBefore = await StockSumAsync(warehouse1, variantId);
        var destBefore = await StockSumAsync(warehouse2, variantId);

        long transferId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            transferId = await sender.Send(new CreateStockTransferCommand(warehouse1, warehouse2, variantId, 3));
        }

        Assert.Equal(sourceBefore, await StockSumAsync(warehouse1, variantId));

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReceiveStockTransferCommand(transferId));
        }

        var sourceAfter = await StockSumAsync(warehouse1, variantId);
        var destAfter = await StockSumAsync(warehouse2, variantId);

        Assert.Equal(sourceBefore - 3, sourceAfter);
        Assert.Equal(destBefore + 3, destAfter);
        Assert.Equal(sourceBefore + destBefore, sourceAfter + destAfter);
    }

    [Fact]
    public async Task Receiving_transfer_twice_throws()
    {
        var (branch1, warehouse1, warehouse2, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        long transferId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            transferId = await sender.Send(new CreateStockTransferCommand(warehouse1, warehouse2, variantId, 2));
            await sender.Send(new ReceiveStockTransferCommand(transferId));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new ReceiveStockTransferCommand(transferId)));
        }
    }
}
