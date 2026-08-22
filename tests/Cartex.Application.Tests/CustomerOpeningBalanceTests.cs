using Cartex.Application.Common.Messaging;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// QARZ-23: boshlang'ich qoldiq defterga yozadi, shuning uchun u `customers.create` bilan emas,
/// o'zining ruxsati bilan ochiladi. Testlar hujjat qoidasidan yozilgan.
[Collection("database")]
public class CustomerOpeningBalanceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Branch, long Business, long Admin);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new Setup(
            (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id,
            (await db.Businesses.FirstAsync()).Id,
            (await db.Users.FirstAsync(x => x.Username == "admin")).Id);
    }

    private void AsUser(Setup s, params string[] permissions)
    {
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        foreach (var permission in permissions)
            Fixture.CurrentUser.Granted.Add(permission);
    }

    private Task<long> CreateAsync(decimal openingBalance) =>
        Send(x => x.Send(new CreateCustomerCommand(
            "Qoldiqli Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            OpeningBalance: openingBalance)));

    private async Task<decimal> BalanceAsync(long customerId, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Type == type))?.Balance ?? 0m;
    }

    private async Task<int> CustomerCountAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Customers.CountAsync();
    }

    private async Task<T> Send<T>(Func<ISender, Task<T>> action)
    {
        using var scope = Fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    // QARZ-23: `customers.create` boshlang'ich qoldiq uchun yetarli emas.
    [Fact]
    public async Task Creating_a_customer_in_debt_without_the_opening_balance_permission_is_refused()
    {
        var s = await SetupAsync();
        AsUser(s, AppPermissions.Customers.Create, AppPermissions.Customers.View);
        var before = await CustomerCountAsync();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(500_000m));

        Assert.Equal("opening_balance_forbidden", error.Code);
        Assert.Equal(before, await CustomerCountAsync());
    }

    // QARZ-23: manfiy qoldiq do'konni qarzdor qiladi — u ham bir xil eshikdan o'tadi.
    [Fact]
    public async Task Creating_a_customer_the_shop_owes_without_the_permission_is_refused()
    {
        var s = await SetupAsync();
        AsUser(s, AppPermissions.Customers.Create, AppPermissions.Customers.View);
        var before = await CustomerCountAsync();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(-500_000m));

        Assert.Equal("opening_balance_forbidden", error.Code);
        Assert.Equal(before, await CustomerCountAsync());
    }

    // QARZ-23: qoldiqsiz mijoz yaratish oddiy ma'lumot kiritish — qo'shimcha ruxsat talab qilmaydi.
    [Fact]
    public async Task A_customer_without_an_opening_balance_needs_no_extra_permission()
    {
        var s = await SetupAsync();
        AsUser(s, AppPermissions.Customers.Create, AppPermissions.Customers.View);

        var id = await CreateAsync(0m);

        Assert.True(id > 0);
        Assert.Equal(0m, await BalanceAsync(id, AccountType.Debt));
    }

    // QARZ-23: ruxsat berilganda qoldiq defterga tushadi — musbat qarz bo'lib.
    [Fact]
    public async Task The_permission_lets_a_debt_opening_balance_through()
    {
        var s = await SetupAsync();
        AsUser(s, AppPermissions.Customers.Create, AppPermissions.Customers.View,
            AppPermissions.Customers.OpeningBalance);

        var id = await CreateAsync(500_000m);

        Assert.Equal(500_000m, await BalanceAsync(id, AccountType.Debt));
    }

    // QARZ-23: manfiy qoldiq mijoz avansiga yoziladi.
    [Fact]
    public async Task The_permission_lets_an_advance_opening_balance_through()
    {
        var s = await SetupAsync();
        AsUser(s, AppPermissions.Customers.Create, AppPermissions.Customers.View,
            AppPermissions.Customers.OpeningBalance);

        var id = await CreateAsync(-500_000m);

        Assert.Equal(500_000m, await BalanceAsync(id, AccountType.CustomerAdvance));
    }
}
