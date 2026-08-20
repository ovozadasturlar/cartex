using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class RepayOverpayPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Debt = 100_000m;
    private const decimal Paid = 150_000m;
    private const decimal Overpay = Paid - Debt;

    private sealed record Ctx(long Branch, long Warehouse, long Business, long Admin);

    private async Task<Ctx> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ctx = new Ctx(
            (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id,
            (await db.Warehouses.FirstAsync(x => x.Name == "Asosiy ombor")).Id,
            (await db.Businesses.FirstAsync()).Id,
            (await db.Users.FirstAsync(x => x.Username == "admin")).Id);
        Fixture.CurrentUser.AsAdmin(ctx.Admin, ctx.Business, ctx.Branch);
        await TestShift.OpenAsync(Fixture);
        return ctx;
    }

    private async Task SetPolicyAsync(bool allowCustomerCredit)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowCustomerCredit = allowCustomerCredit });
    }

    private async Task<long> CreateDebtorAsync()
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerCommand("Ortiqcha to'lov mijozi",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
                CreditLimit: 10_000_000m, OpeningBalance: Debt));
    }

    private async Task<CustomerPaymentCreatedDto> PayAsync(Ctx ctx, long customerId, decimal amount)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerPaymentCommand(customerId, ctx.Branch,
                [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", amount)]));
    }

    private async Task<decimal> BalanceAsync(long customerId, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == type)
            .SumAsync(x => x.Balance);
    }

    // QARZ-20
    [Fact]
    public async Task QARZ_20_Online_overpayment_is_refused_while_customer_credit_is_off()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowCustomerCredit: false);
        var customerId = await CreateDebtorAsync();
        Assert.Equal(Debt, await BalanceAsync(customerId, AccountType.Debt));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => PayAsync(ctx, customerId, Paid));
        Assert.Equal("payment_exceeds_debt", error.Code);

        Assert.Equal(Debt, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.CustomerAdvance));
        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.CustomerPaymentDocuments.CountAsync(x => x.CustomerId == customerId));
    }

    // QARZ-20
    [Fact]
    public async Task QARZ_20_Online_overpayment_becomes_advance_while_customer_credit_is_on()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowCustomerCredit: true);
        var customerId = await CreateDebtorAsync();

        var document = await PayAsync(ctx, customerId, Paid);

        Assert.Equal(Paid, document.TotalBaseAmount);
        Assert.Equal(Debt, document.AllocatedBaseAmount);
        Assert.Equal(Overpay, document.AdvanceBaseAmount);
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(Overpay, await BalanceAsync(customerId, AccountType.CustomerAdvance));
    }

    // QARZ-20, OFF-21
    [Fact]
    public async Task QARZ_20_Offline_replay_keeps_the_overpayment_even_while_customer_credit_is_off()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowCustomerCredit: false);
        var customerId = await CreateDebtorAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant, TestOffline.Event(1, "customer.payment.create",
            new CreateCustomerPaymentRequest(customerId, ctx.Branch,
                [new CustomerPaymentTenderRequest("Cash", "UZS", Paid)])));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await db.CustomerPaymentDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == applied.ResultEntityId);
        Assert.Equal(Paid, document.TotalBaseAmount);
        Assert.Equal(Debt, document.AllocatedBaseAmount);
        Assert.Equal(Overpay, document.AdvanceBaseAmount);
        Assert.Equal(0m, await BalanceAsync(customerId, AccountType.Debt));
        Assert.Equal(Overpay, await BalanceAsync(customerId, AccountType.CustomerAdvance));
    }
}
