using Cartex.Application.Sales.Commands;
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
public class CreateSaleTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Coca-Cola 1.5L")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    [Fact]
    public async Task Sale_decrements_stock_posts_ledger_and_writes_outbox()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();

        decimal stockBefore, cashBefore;
        int outboxBefore;
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockBefore = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            cashBefore = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            outboxBefore = await db.NotificationOutbox.CountAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, null, 20000, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stockAfter = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            var cashAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            var outboxAfter = await db.NotificationOutbox.CountAsync();

            Assert.Equal(stockBefore - 2, stockAfter);
            Assert.Equal(cashBefore + 20000, cashAfter);
            Assert.True(outboxAfter > outboxBefore);
        }
    }

    [Fact]
    public async Task Sale_with_insufficient_stock_throws_and_keeps_stock()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        decimal before;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            before = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, null, 1_000_000_000m, 0, 0, [new CreateSaleItemDto(variantId, before + 1000)])));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var after = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            Assert.Equal(before, after);
        }
    }
}
