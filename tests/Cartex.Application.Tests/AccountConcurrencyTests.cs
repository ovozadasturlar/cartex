using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class AccountConcurrencyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long adminId, long businessId, long variantA, long variantB)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var unitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;

        async Task<long> MakeProductAsync(string name)
        {
            var variant = new ProductVariant { Product = new Product { Name = name, UnitId = unitId, MinStock = 0, IsEnabled = true }, IsDefault = true };
            db.ProductVariants.Add(variant);
            await db.SaveChangesAsync();
            db.ProductPrices.Add(new ProductPrice { VariantId = variant.Id, WarehouseId = null, SellingPrice = 385000, Currency = "UZS" });
            scope.ServiceProvider.GetRequiredService<InventoryReasonState>().Declare(
                new(InventoryMovementKind.Adjustment, "TestSetup", null, InventoryLocation.External()));
            db.Stocks.Add(new Stock { BranchId = branch1, WarehouseId = warehouse1, VariantId = variant.Id, Quantity = 100, PurchasePrice = 100 });
            await db.SaveChangesAsync();
            return variant.Id;
        }

        var variantA = await MakeProductAsync("Kassa konkurentlik A");
        var variantB = await MakeProductAsync("Kassa konkurentlik B");
        return (branch1, warehouse1, adminId, businessId, variantA, variantB);
    }

    private async Task<bool> SellCashAsync(long warehouseId, long variantId)
    {
        try
        {
            using var scope = Fixture.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouseId, null, 385000, 0, 0, [new CreateSaleItemDto(variantId, 1)]));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<decimal> CashBalanceAsync(long branchId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(a => a.BranchId == branchId && a.Type == AccountType.Cash && a.Currency == "UZS")
            .Select(a => a.Balance).FirstAsync();
    }

    [Fact]
    public async Task Concurrent_cash_sales_do_not_lose_balance_updates()
    {
        var (branch1, warehouse1, adminId, businessId, variantA, variantB) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var before = await CashBalanceAsync(branch1);

        var results = await Task.WhenAll(
            SellCashAsync(warehouse1, variantA),
            SellCashAsync(warehouse1, variantB));

        Assert.All(results, ok => Assert.True(ok));

        var after = await CashBalanceAsync(branch1);
        Assert.Equal(770000m, after - before);
    }
}
