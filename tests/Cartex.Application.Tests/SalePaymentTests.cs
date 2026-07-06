using Cartex.Application.Customers.Commands;
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
public class SalePaymentTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Coca-Cola 1.5L")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    private async Task<long> CreateCustomerAsync(decimal creditLimit)
    {
        using var scope = fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCustomerCommand("Mijoz", "+9989" + Guid.NewGuid().ToString("N")[..8], null, 0m, CreditLimit: creditLimit));
    }

    private async Task<decimal> AccountBalanceAsync(Func<ApplicationDbContext, Task<decimal>> read)
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await read(db);
    }

    [Fact]
    public async Task Mixed_payment_splits_across_cash_card_and_debt()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(fixture);

        var total = price * 2;
        var cashPaid = 1000m;
        var cardPaid = 2000m;
        var expectedDebt = total - cashPaid - cardPaid;
        var customerId = await CreateCustomerAsync(total);

        var cashBefore = await AccountBalanceAsync(db =>
            db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Cash).Select(a => a.Balance).FirstOrDefaultAsync());
        var cardBefore = await AccountBalanceAsync(db =>
            db.Accounts.Where(a => a.BranchId == branch1 && a.Type == AccountType.Card).Select(a => a.Balance).FirstOrDefaultAsync());

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, cashPaid, cardPaid, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using var check = fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.CustomerId == customerId);
        var cashAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
        var cardAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Card)).Balance;
        var debt = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt)).Balance;

        Assert.Equal(total, sale.TotalAmount);
        Assert.Equal(expectedDebt, sale.DebtAmount);
        Assert.Equal(cashBefore + cashPaid, cashAfter);
        Assert.Equal(cardBefore + cardPaid, cardAfter);
        Assert.Equal(expectedDebt, debt);
        Assert.Equal(sale.TotalAmount, sale.PaidCash + sale.PaidCard + sale.PaidBonus + sale.DebtAmount);
    }

    [Fact]
    public async Task Debt_at_exactly_credit_limit_is_allowed_then_over_limit_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(fixture);

        var limit = price * 2;
        var customerId = await CreateCustomerAsync(limit);

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1)])));
        }

        using var check = fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var debt = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt)).Balance;
        Assert.Equal(limit, debt);
    }

    [Fact]
    public async Task Paying_more_bonus_than_balance_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(fixture);

        var customerId = await CreateCustomerAsync(0m);

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GiveCustomerBonusCommand(customerId, 1000m, null));
        }

        using (var scope = fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, customerId, price * 2 - 2000m, 0, 2000m, [new CreateSaleItemDto(variantId, 2)])));
        }

        using var check = fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bonus = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Bonus)).Balance;
        Assert.Equal(1000m, bonus);
    }
}
