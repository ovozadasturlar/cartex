using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Stocks.Commands;
using Cartex.Application.Stocks.Queries;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class InventoryJournalTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long BranchId, long Branch2Id, long WarehouseId, long Warehouse2Id, long BusinessId, long AdminId, long SellerId);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
        var branch2 = await db.Branches.Where(x => x.Name == "Filial 2").Select(x => x.Id).FirstAsync();
        return new Setup(
            branch1,
            branch2,
            await db.Warehouses.Where(x => x.BranchId == branch1).Select(x => x.Id).FirstAsync(),
            await db.Warehouses.Where(x => x.BranchId == branch2).Select(x => x.Id).FirstAsync(),
            await db.Businesses.Select(x => x.Id).FirstAsync(),
            await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync(),
            await db.Users.Where(x => x.Username == "seller").Select(x => x.Id).FirstAsync());
    }

    private async Task<long> CreateVariantAsync(string name, decimal sellingPrice)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(x => x.IsDefault).Select(x => x.Id).FirstAsync();
        var productId = await sender.Send(new CreateProductCommand(name, null, unitId, null, null, SellingPrice: sellingPrice));
        return await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
    }

    private async Task ReceiveAsync(long warehouseId, long variantId, decimal quantity, decimal purchasePrice)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateSupplyCommand(null, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, quantity, purchasePrice, null)]));
    }

    private async Task<long> SellAsync(long warehouseId, long variantId, decimal quantity, decimal cash)
    {
        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateSaleCommand(warehouseId, null, cash, 0, 0, [new CreateSaleItemDto(variantId, quantity)]));
        return result.SaleId;
    }

    private async Task ReturnAsync(long saleId, decimal quantity, bool restock)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var itemId = await db.SaleItems.Where(x => x.SaleId == saleId).Select(x => x.Id).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            await TestReturns.ForItemAsync(db, itemId, quantity, restock));
    }

    private async Task<decimal> StockAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId).SumAsync(x => x.Quantity);
    }

    private async Task<List<InventoryMovement>> MovementsAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.InventoryMovements.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task OMBOR_01_OMBOR_05_Receive_sell_return_and_scrap_write_four_movements_summing_to_stock()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var variantId = await CreateVariantAsync("Ombor jurnali to'liqligi", 10_000m);

        Assert.Equal(0m, await StockAsync(setup.WarehouseId, variantId));
        Assert.Empty(await MovementsAsync(setup.WarehouseId, variantId));

        await ReceiveAsync(setup.WarehouseId, variantId, 50m, 5_000m);
        var saleId = await SellAsync(setup.WarehouseId, variantId, 3m, 30_000m);
        await ReturnAsync(saleId, 1m, restock: true);

        using (var scope = Fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new AdjustStockCommand(setup.WarehouseId, variantId, 46m, "brak"));

        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        var stock = await StockAsync(setup.WarehouseId, variantId);

        Assert.Equal(4, movements.Count);
        Assert.Equal([50m, -3m, 1m, -2m], movements.Select(x => x.Quantity));
        Assert.Equal(46m, movements.Sum(x => x.Quantity));
        Assert.Equal(46m, stock);
        Assert.Equal(stock, movements.Sum(x => x.Quantity));
    }

    [Fact]
    public async Task OMBOR_02_Written_movement_cannot_be_edited_or_deleted()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        var variantId = await CreateVariantAsync("Ombor jurnali o'zgarmasligi", 10_000m);
        await ReceiveAsync(setup.WarehouseId, variantId, 10m, 5_000m);

        var original = Assert.Single(await MovementsAsync(setup.WarehouseId, variantId));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var movement = await db.InventoryMovements.SingleAsync(x => x.Id == original.Id);
            movement.Quantity = 999m;
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var movement = await db.InventoryMovements.SingleAsync(x => x.Id == original.Id);
            db.InventoryMovements.Remove(movement);
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        var kept = Assert.Single(await MovementsAsync(setup.WarehouseId, variantId));
        Assert.Equal(original.Id, kept.Id);
        Assert.Equal(10m, kept.Quantity);
    }

    [Fact]
    public async Task OMBOR_02_Voiding_a_sale_keeps_the_original_movement_and_adds_an_opposite_one()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var variantId = await CreateVariantAsync("Ombor jurnali bekor qilish", 10_000m);
        await ReceiveAsync(setup.WarehouseId, variantId, 10m, 5_000m);
        var saleId = await SellAsync(setup.WarehouseId, variantId, 2m, 20_000m);

        var issue = Assert.Single(await MovementsAsync(setup.WarehouseId, variantId), x => x.Kind == InventoryMovementKind.SaleIssue);
        Assert.Equal(-2m, issue.Quantity);

        using (var scope = Fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new VoidSaleCommand(saleId, "xato savdo"));

        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        var stillThere = Assert.Single(movements, x => x.Id == issue.Id);
        Assert.Equal(-2m, stillThere.Quantity);

        var reversal = Assert.Single(movements, x => x.Kind == InventoryMovementKind.SaleVoid);
        Assert.Equal(2m, reversal.Quantity);
        Assert.Equal(3, movements.Count);
        Assert.Equal(await StockAsync(setup.WarehouseId, variantId), movements.Sum(x => x.Quantity));
    }

    [Fact]
    public async Task OMBOR_03_Stock_change_without_a_declared_reason_is_refused_and_writes_nothing()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        var variantId = await CreateVariantAsync("Ombor jurnali sababsiz", 10_000m);
        await ReceiveAsync(setup.WarehouseId, variantId, 10m, 5_000m);

        int movementsBefore;
        using (var scope = Fixture.CreateScope())
            movementsBefore = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().InventoryMovements.CountAsync();

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stock = await db.Stocks.SingleAsync(x => x.WarehouseId == setup.WarehouseId && x.VariantId == variantId);
            stock.Quantity += 7m;
            var error = await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }

        Assert.Equal(10m, await StockAsync(setup.WarehouseId, variantId));

        using var check = Fixture.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(movementsBefore, await verify.InventoryMovements.CountAsync());
        Assert.False(await verify.InventoryMovements.AnyAsync(x => x.SourceType == "" || x.SourceType == "Unknown" || x.SourceType == "Other"));
        Assert.DoesNotContain(Enum.GetNames<InventoryMovementKind>(), x => x is "Unknown" or "Other");
    }

    [Fact]
    public async Task OMBOR_04_Quarantine_balance_is_derived_from_movements_and_never_stored()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var variantId = await CreateVariantAsync("Ombor jurnali karantin", 10_000m);
        await ReceiveAsync(setup.WarehouseId, variantId, 10m, 5_000m);
        var saleId = await SellAsync(setup.WarehouseId, variantId, 1m, 10_000m);

        var sellableAfterSale = await StockAsync(setup.WarehouseId, variantId);
        await ReturnAsync(saleId, 1m, restock: false);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var quarantined = -await db.InventoryMovements
            .Where(x => x.WarehouseId == setup.WarehouseId
                        && x.VariantId == variantId
                        && (x.ToLocationKind == InventoryLocationKind.Quarantine
                            || x.FromLocationKind == InventoryLocationKind.Quarantine))
            .SumAsync(x => x.Quantity);

        Assert.Equal(1m, quarantined);
        Assert.Equal(sellableAfterSale, await StockAsync(setup.WarehouseId, variantId));

        var movements = await MovementsAsync(setup.WarehouseId, variantId);
        Assert.Equal(await StockAsync(setup.WarehouseId, variantId), movements.Sum(x => x.Quantity));

        var storedCounters = db.Model.GetEntityTypes()
            .SelectMany(x => x.GetProperties())
            .Select(x => $"{x.DeclaringType.ShortName()}.{x.Name}")
            .Where(x => x.Contains("Quarantine", StringComparison.OrdinalIgnoreCase)
                        || x.Contains("Scrap", StringComparison.OrdinalIgnoreCase)
                        || x.Contains("SupplierClaim", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(storedCounters);
    }

    [Fact]
    public async Task OMBOR_06_Movement_history_is_scoped_to_the_users_branches()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId, setup.Branch2Id);
        var variantId = await CreateVariantAsync("Ombor jurnali filial", 10_000m);
        await ReceiveAsync(setup.WarehouseId, variantId, 4m, 5_000m);
        await ReceiveAsync(setup.Warehouse2Id, variantId, 6m, 5_000m);

        Fixture.CurrentUser.AsCashier(setup.SellerId, setup.BusinessId, setup.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Stocks.View);

        using (var scope = Fixture.CreateScope())
        {
            var visible = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GetInventoryMovementsQuery(variantId, null));
            Assert.NotEmpty(visible);
            Assert.All(visible, x => Assert.Equal(setup.WarehouseId, x.WarehouseId));
            Assert.DoesNotContain(visible, x => x.WarehouseId == setup.Warehouse2Id);
        }

        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId, setup.Branch2Id);

        using (var scope = Fixture.CreateScope())
        {
            var visible = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GetInventoryMovementsQuery(variantId, null));
            Assert.Contains(visible, x => x.WarehouseId == setup.Warehouse2Id);
        }
    }
}
