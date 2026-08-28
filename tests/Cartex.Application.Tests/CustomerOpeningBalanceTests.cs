using Cartex.Application.Common.Messaging;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// QARZ-24: hali hech qanday operatsiya bo'lmagan mijoz - kiritish xatosi, biznes tarixi emas.
[Collection("database")]
public sealed class CustomerOpeningBalanceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task QARZ_24_Opening_debt_can_be_corrected_while_the_customer_is_untouched()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);

        await UpdateAsync(scope, customerId, openingBalance: 300_000m);

        Assert.Equal(300_000m, await DebtAsync(scope, customerId));
        Assert.Equal(1, await OpeningEntryCountAsync(scope, customerId));
    }

    [Fact]
    public async Task QARZ_24_Correction_can_flip_debt_into_credit()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);

        await UpdateAsync(scope, customerId, openingBalance: -200_000m);

        Assert.Equal(0m, await DebtAsync(scope, customerId));
        Assert.Equal(200_000m, await BalanceAsync(scope, customerId, AccountType.CustomerAdvance));
    }

    [Fact]
    public async Task QARZ_24_Correction_to_zero_leaves_no_opening_entry()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);

        await UpdateAsync(scope, customerId, openingBalance: 0m);

        Assert.Equal(0m, await DebtAsync(scope, customerId));
        Assert.Equal(0, await OpeningEntryCountAsync(scope, customerId));
    }

    [Fact]
    public async Task QARZ_24_Untouched_customer_is_deleted_together_with_the_opening_entry()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);

        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteCustomerCommand(customerId));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();
        Assert.True(await db.Customers.IgnoreQueryFilters().Where(x => x.Id == customerId).Select(x => x.IsDeleted).SingleAsync());
        Assert.Equal(0m, await DebtAsync(scope, customerId));
        Assert.Equal(0, await OpeningEntryCountAsync(scope, customerId));
    }

    [Fact]
    public async Task QARZ_24_Activity_closes_both_the_correction_and_the_deletion()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);
        await AddSaleAsync(scope, customerId);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var correction = await Assert.ThrowsAsync<BusinessRuleException>(
            () => UpdateAsync(scope, customerId, openingBalance: 100_000m));
        var deletion = await Assert.ThrowsAsync<BusinessRuleException>(
            () => sender.Send(new DeleteCustomerCommand(customerId)));

        Assert.Equal("customer_has_activity", correction.Code);
        Assert.Equal("customer_balance_open", deletion.Code);
        Assert.Equal(500_000m, await DebtAsync(scope, customerId));
    }

    [Fact]
    public async Task QARZ_24_Reading_a_customer_reports_whether_the_opening_balance_is_still_editable()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var before = await sender.Send(new GetCustomerByIdQuery(customerId));
        await AddSaleAsync(scope, customerId);
        var after = await sender.Send(new GetCustomerByIdQuery(customerId));

        Assert.True(before!.IsUntouched);
        Assert.Equal(500_000m, before.OpeningBalance);
        Assert.False(after!.IsUntouched);
        Assert.Equal(0m, after.OpeningBalance);
    }

    /// QARZ-21: bonus pul emas - uning yozuvi boshlang'ich qoldiq tuzatishida yo'qolmaydi.
    [Fact]
    public async Task QARZ_24_Correction_does_not_touch_the_bonus_ledger()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new GiveCustomerBonusCommand(customerId, 5_000m, "sovg'a"));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => UpdateAsync(scope, customerId, openingBalance: 100_000m));

        Assert.Equal("customer_has_activity", error.Code);
        Assert.Equal(5_000m, await BalanceAsync(scope, customerId, AccountType.Bonus));
        Assert.Equal(500_000m, await DebtAsync(scope, customerId));
    }

    /// QARZ-24: tugallanmagan savat amal emas - o'chirishni to'smaydi va mijoz bilan yopiladi.
    [Fact]
    public async Task QARZ_24_An_unfinished_cart_does_not_block_the_deletion()
    {
        using var scope = await LoginAsync();
        var customerId = await CreateAsync(scope, openingBalance: 500_000m);
        await AddCartAsync(scope, customerId);

        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteCustomerCommand(customerId));

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();
        Assert.False(await db.Carts.AnyAsync(x => x.CustomerId == customerId));
        Assert.Equal(0m, await DebtAsync(scope, customerId));
    }

    private async Task<IServiceScope> LoginAsync()
    {
        var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        return scope;
    }

    private static Task<long> CreateAsync(IServiceScope scope, decimal openingBalance) =>
        scope.ServiceProvider.GetRequiredService<ISender>().Send(new CreateCustomerCommand(
            "Toza mijoz", "+998901112233", null, 0) { OpeningBalance = openingBalance });

    private static Task UpdateAsync(IServiceScope scope, long customerId, decimal openingBalance) =>
        scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCustomerCommand(
            customerId, "Toza mijoz", "+998901112233", null, 0) { OpeningBalance = openingBalance });

    private static Task<decimal> DebtAsync(IServiceScope scope, long customerId) =>
        BalanceAsync(scope, customerId, AccountType.Debt);

    private static async Task<decimal> BalanceAsync(IServiceScope scope, long customerId, AccountType type)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();
        return await db.Accounts.Where(x => x.CustomerId == customerId && x.Type == type)
            .SumAsync(x => (decimal?)x.Balance) ?? 0m;
    }

    private static async Task<int> OpeningEntryCountAsync(IServiceScope scope, long customerId)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();
        return await db.Transactions.CountAsync(x => x.Description == "Boshlang'ich qoldiq"
            && ((x.FromAccount != null && x.FromAccount.CustomerId == customerId)
                || (x.ToAccount != null && x.ToAccount.CustomerId == customerId)));
    }

    private static async Task AddCartAsync(IServiceScope scope, long customerId)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var warehouse = await db.Warehouses.FirstAsync();
        db.Carts.Add(new Cart
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            CustomerId = customerId,
            AggregateCode = Guid.NewGuid().ToString("N")[..8]
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    /// Savdo yozuvi mijozga biznes tarixi paydo bo'lganini bildiradi.
    private static async Task AddSaleAsync(IServiceScope scope, long customerId)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var warehouse = await db.Warehouses.FirstAsync();
        var userId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        db.Sales.Add(new Sale
        {
            BranchId = warehouse.BranchId,
            WarehouseId = warehouse.Id,
            CustomerId = customerId,
            UserId = userId,
            DocumentNumber = Guid.NewGuid().ToString("N")[..10],
            ReceiptToken = Guid.NewGuid().ToString("N")
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
