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
public class ReturnSaleTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    [Fact]
    public async Task Return_restores_stock_reverses_cash_and_marks_returned()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        decimal stockBefore, cashBefore;
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockBefore = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            cashBefore = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, 770000, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lines = await db.SaleItems.Where(i => i.SaleId == saleId)
                .Select(i => new ReturnLineDto(i.Id, i.Quantity, true, null)).ToListAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, lines));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stockAfter = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            var cashAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            var status = (await db.Sales.FirstAsync(s => s.Id == saleId)).Status;

            Assert.Equal(stockBefore, stockAfter);
            Assert.Equal(cashBefore, cashAfter);
            Assert.Equal(SaleStatus.Returned, status);
        }
    }

    [Fact]
    public async Task Return_twice_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long saleId;
        List<ReturnLineDto> lines;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, 385000, 0, 0, [new CreateSaleItemDto(variantId, 1)]))).SaleId;
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            lines = await db.SaleItems.Where(i => i.SaleId == saleId)
                .Select(i => new ReturnLineDto(i.Id, i.Quantity, true, null)).ToListAsync();
            await sender.Send(new ReturnSaleCommand(saleId, lines));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new ReturnSaleCommand(saleId, lines)));
        }
    }
}
