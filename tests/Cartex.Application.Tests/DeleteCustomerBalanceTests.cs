using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class DeleteCustomerBalanceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const string BalanceOpen = "customer_balance_open";
    private const decimal Debt = 50_000m;
    private const decimal Bonus = 5_000m;

    private async Task StartAsync()
    {
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        }
        await TestShift.OpenAsync(Fixture);
    }

    private async Task<long> CreateCustomerAsync(decimal openingBalance = 0m)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerCommand(
            "Hisobli mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: 10_000_000m, OpeningBalance: openingBalance));
    }

    private async Task AllowCustomerCreditAsync()
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowCustomerCredit = true });
    }

    private async Task RepayAsync(long customerId, decimal amount)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new RepayCustomerDebtCommand(customerId, amount, ViaCard: true));
    }

    private async Task<decimal> BalanceAsync(long customerId, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Type == type))?.Balance ?? 0m;
    }

    private async Task DeleteAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteCustomerCommand(customerId));
    }

    private async Task<bool> ExistsAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Customers.AnyAsync(c => c.Id == customerId);
    }

    // QARZ-21
    [Fact]
    public async Task QARZ_21_customer_with_open_debt_is_not_deleted()
    {
        await StartAsync();
        var customerId = await CreateCustomerAsync(Debt);

        Assert.Equal(Debt, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.CustomerAdvance));

        var error = await Assert.ThrowsAnyAsync<DomainException>(() => DeleteAsync(customerId));

        Assert.Equal(BalanceOpen, error.Code);
        Assert.True(await ExistsAsync(customerId));
    }

    // QARZ-21
    [Fact]
    public async Task QARZ_21_customer_with_advance_is_not_deleted()
    {
        await StartAsync();
        await AllowCustomerCreditAsync();
        var customerId = await CreateCustomerAsync(Debt);
        await RepayAsync(customerId, Debt + 10_000m);

        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(10_000m, await BalanceAsync(customerId, AccountType.CustomerAdvance));

        var error = await Assert.ThrowsAnyAsync<DomainException>(() => DeleteAsync(customerId));

        Assert.Equal(BalanceOpen, error.Code);
        Assert.True(await ExistsAsync(customerId));
    }

    // QARZ-21: a bonus is not money, so it neither blocks the deletion nor gets lost by it.
    [Fact]
    public async Task QARZ_21_customer_with_only_bonus_is_deleted_and_keeps_the_bonus()
    {
        await StartAsync();
        var customerId = await CreateCustomerAsync();

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GiveCustomerBonusCommand(customerId, Bonus, "sovg'a"));
        }

        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.CustomerAdvance));
        Assert.Equal(Bonus, await BalanceAsync(customerId, AccountType.Bonus));

        await DeleteAsync(customerId);

        Assert.False(await ExistsAsync(customerId));
        Assert.Equal(Bonus, await BalanceAsync(customerId, AccountType.Bonus));
    }

    // QARZ-21
    [Fact]
    public async Task QARZ_21_customer_with_settled_balance_is_deleted()
    {
        await StartAsync();
        var customerId = await CreateCustomerAsync(Debt);
        await RepayAsync(customerId, Debt);

        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.CustomerAdvance));

        await DeleteAsync(customerId);

        Assert.False(await ExistsAsync(customerId));
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Customers.IgnoreQueryFilters().Where(c => c.Id == customerId)
            .Select(c => c.IsDeleted).SingleAsync());
    }
}
