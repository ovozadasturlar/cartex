using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class StockConcurrencyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long adminId, long businessId, long variantId)> SetupAsync(decimal stock)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var unitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;

        var product = new Product { Name = "Konkurentlik testi", UnitId = unitId, MinStock = 0, IsEnabled = true };
        var variant = new ProductVariant { Product = product, IsDefault = true };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        db.ProductPrices.Add(new ProductPrice { VariantId = variant.Id, WarehouseId = null, SellingPrice = 385000, Currency = "UZS" });
        scope.ServiceProvider.GetRequiredService<InventoryReasonState>().Declare(
            new(InventoryMovementKind.Adjustment, "TestSetup", null, InventoryLocation.External()));
        db.Stocks.Add(new Stock { BranchId = branch1, WarehouseId = warehouse1, VariantId = variant.Id, Quantity = stock, PurchasePrice = 100 });
        await db.SaveChangesAsync();

        return (branch1, warehouse1, adminId, businessId, variant.Id);
    }

    private async Task<bool> TrySellAsync(long warehouseId, long variantId, decimal quantity)
    {
        try
        {
            using var scope = Fixture.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouseId, null, 385000m * quantity, 0, 0, [new CreateSaleItemDto(variantId, quantity)]));
            return true;
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }

    [Fact]
    public async Task Concurrent_sales_never_oversell_stock()
    {
        var (branch1, warehouse1, adminId, businessId, variantId) = await SetupAsync(stock: 5);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var results = await Task.WhenAll(
            TrySellAsync(warehouse1, variantId, 3),
            TrySellAsync(warehouse1, variantId, 3));

        Assert.Equal(1, results.Count(ok => ok));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var onHand = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
        Assert.Equal(2m, onHand);
    }
}
