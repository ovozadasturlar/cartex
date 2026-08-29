using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class RepayDebtOverpayTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<long> SetupCustomerWithDebtAsync(decimal debt)
    {
        long warehouse1, variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
            var businessId = (await db.Businesses.FirstAsync()).Id;
            var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        }
        await TestShift.OpenAsync(Fixture);

        using var saleScope = Fixture.CreateScope();
        var sender = saleScope.ServiceProvider.GetRequiredService<ISender>();
        var customerId = await sender.Send(new CreateCustomerCommand("Qarzdor Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
        await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1, debt)]));
        return customerId;
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
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new RepayCustomerDebtCommand(customerId, amount, ViaCard: true));
    }

    private async Task<decimal> BalanceAsync(long customerId, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == type))?.Balance ?? 0m;
    }

    // QARZ-03, QARZ-20
    [Fact]
    public async Task QARZ_03_repay_overpay_goes_to_advance()
    {
        var customerId = await SetupCustomerWithDebtAsync(50_000m);
        await AllowCustomerCreditAsync();

        await RepayAsync(customerId, 60_000m);

        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(10_000m, await BalanceAsync(customerId, AccountType.CustomerAdvance));
    }

    // QARZ-03
    [Fact]
    public async Task QARZ_03_repay_exact_amount_closes_debt()
    {
        var customerId = await SetupCustomerWithDebtAsync(50_000m);

        await RepayAsync(customerId, 50_000m);

        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.CustomerAdvance));
    }
}
