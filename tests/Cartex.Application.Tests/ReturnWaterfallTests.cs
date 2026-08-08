using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ReturnWaterfallTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    private async Task<decimal> BalanceAsync(Func<ApplicationDbContext, IQueryable<Account>> filter)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await filter(db).Select(a => a.Balance).FirstOrDefaultAsync();
    }

    [Fact]
    public async Task Partial_return_refunds_debt_before_cash()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long customerId, saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Waterfall Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, 1000m, 2000m, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        var debtBefore = await BalanceAsync(db => db.Accounts.Where(a => a.CustomerId == customerId && a.Type == AccountType.Debt));
        var cashBefore = await BalanceAsync(db => db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash));
        var cardBefore = await BalanceAsync(db => db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Card));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.SaleItems.FirstAsync(i => i.SaleId == saleId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, [new ReturnLineDto(item.Id, 1, true, null)]));
        }

        var expectedDebtTake = Math.Min(price, debtBefore);
        Assert.Equal(debtBefore - expectedDebtTake, await BalanceAsync(db => db.Accounts.Where(a => a.CustomerId == customerId && a.Type == AccountType.Debt)));
        Assert.Equal(cashBefore, await BalanceAsync(db => db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash)));
        Assert.Equal(cardBefore, await BalanceAsync(db => db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Card)));

        using var check = Fixture.CreateScope();
        var sale = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Sales.FirstAsync(s => s.Id == saleId);
        Assert.Equal(expectedDebtTake, sale.RefundedDebt);
        Assert.Equal(0m, sale.RefundedCash);
        Assert.Equal(SaleStatus.PartialReturn, sale.Status);
    }

    [Fact]
    public async Task Full_return_after_partial_clears_everything_exactly()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long customerId, saleId;
        decimal cashBeforeSale, cardBeforeSale;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            cashBeforeSale = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash).Select(a => a.Balance).FirstOrDefaultAsync();
            cardBeforeSale = await db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Card).Select(a => a.Balance).FirstOrDefaultAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Waterfall Full", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, 1000m, 2000m, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.SaleItems.FirstAsync(i => i.SaleId == saleId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, [new ReturnLineDto(item.Id, 1, true, null)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.SaleItems.FirstAsync(i => i.SaleId == saleId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, [new ReturnLineDto(item.Id, 1, true, null)]));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db2.Sales.FirstAsync(s => s.Id == saleId);
        var debt = await db2.Accounts.Where(a => a.CustomerId == customerId && a.Type == AccountType.Debt).Select(a => a.Balance).FirstOrDefaultAsync();
        var cash = await db2.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash).Select(a => a.Balance).FirstOrDefaultAsync();
        var card = await db2.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Card).Select(a => a.Balance).FirstOrDefaultAsync();

        Assert.Equal(SaleStatus.Returned, sale.Status);
        Assert.Equal(sale.TotalAmount, sale.RefundedCash + sale.RefundedCard + sale.RefundedBonus + sale.RefundedDebt);
        Assert.Equal(0m, debt);
        Assert.Equal(cashBeforeSale, cash);
        Assert.Equal(cardBeforeSale, card);
    }

    [Fact]
    public async Task Cashback_reversal_never_drives_bonus_negative()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long customerId, saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.LoyaltyPrograms.Add(new LoyaltyProgram { IsEnabled = true, TotalPercent = 10 });
            await db.SaveChangesAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("Cashback Cap", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m));
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, price * 2, 0, 0, [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        decimal earned;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            earned = (await db.Sales.FirstAsync(s => s.Id == saleId)).CashbackEarned;
            Assert.True(earned > 0);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, price - earned, 0, earned, [new CreateSaleItemDto(variantId, 1)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var items = await db.SaleItems.Where(i => i.SaleId == saleId).ToListAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, [.. items.Select(i => new ReturnLineDto(i.Id, i.Quantity, true, null))]));
        }

        var bonus = await BalanceAsync(db => db.Accounts.Where(a => a.CustomerId == customerId && a.Type == AccountType.Bonus));
        Assert.True(bonus >= 0);
    }
}
