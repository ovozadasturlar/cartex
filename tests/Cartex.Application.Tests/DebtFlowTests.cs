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
public class DebtFlowTests(DatabaseFixture fixture) : DatabaseTest(fixture)
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

    private async Task<long> CreateCustomerAsync(decimal creditLimit = 10_000_000m)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCustomerCommand("Qarzdor Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: creditLimit));
    }

    private async Task CreditSaleAsync(long warehouse, long variantId, long customerId, decimal qty)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CreateSaleCommand(warehouse, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, qty)]));
    }

    private async Task<decimal> DebtBalanceAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt)).Balance;
    }

    private async Task<decimal> BranchBalanceAsync(long branch, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.BranchId == branch && a.Type == type);
        return account?.Balance ?? 0m;
    }

    [Fact]
    public async Task Credit_sale_increases_customer_debt_by_total()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        await CreditSaleAsync(warehouse1, variantId, customerId, 3);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.CustomerId == customerId);
        var debt = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt)).Balance;

        Assert.Equal(sale.TotalAmount, sale.DebtAmount);
        Assert.Equal(sale.TotalAmount, debt);
    }

    [Fact]
    public async Task Repay_reduces_debt_and_increases_cash()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        await CreditSaleAsync(warehouse1, variantId, customerId, 2);

        var debt = await DebtBalanceAsync(customerId);
        var cashBefore = await BranchBalanceAsync(branch1, AccountType.Cash);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new RepayCustomerDebtCommand(customerId, debt, false));
        }

        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(cashBefore + debt, await BranchBalanceAsync(branch1, AccountType.Cash));
    }

    [Fact]
    public async Task Repay_via_card_hits_card_account_not_cash()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        await CreditSaleAsync(warehouse1, variantId, customerId, 2);

        var debt = await DebtBalanceAsync(customerId);
        var cashBefore = await BranchBalanceAsync(branch1, AccountType.Cash);
        var cardBefore = await BranchBalanceAsync(branch1, AccountType.Card);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new RepayCustomerDebtCommand(customerId, debt, true));
        }

        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(cashBefore, await BranchBalanceAsync(branch1, AccountType.Cash));
        Assert.Equal(cardBefore + debt, await BranchBalanceAsync(branch1, AccountType.Card));
    }

    [Fact]
    public async Task Repay_more_than_debt_throws_and_keeps_balance()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        await CreditSaleAsync(warehouse1, variantId, customerId, 2);
        var debt = await DebtBalanceAsync(customerId);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new RepayCustomerDebtCommand(customerId, debt + 1m, false)));
        }

        Assert.Equal(debt, await DebtBalanceAsync(customerId));
    }

    [Fact]
    public async Task Opening_balance_posts_customer_debt()
    {
        var (branch1, _, businessId, adminId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        long debtorId, creditorId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            debtorId = await sender.Send(new CreateCustomerCommand("Eski Qarzdor", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, OpeningBalance: 50_000m));
            creditorId = await sender.Send(new CreateCustomerCommand("Haqdor Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, OpeningBalance: -30_000m));
        }

        Assert.Equal(50_000m, await DebtBalanceAsync(debtorId));
        Assert.Equal(-30_000m, await DebtBalanceAsync(creditorId));
    }

    [Fact]
    public async Task Give_bonus_increases_bonus_balance()
    {
        var (branch1, _, businessId, adminId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GiveCustomerBonusCommand(customerId, 5000m, "sovg'a"));
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bonus = (await db.Accounts.FirstAsync(a => a.CustomerId == customerId && a.Type == AccountType.Bonus)).Balance;
        Assert.Equal(5000m, bonus);
    }
}
