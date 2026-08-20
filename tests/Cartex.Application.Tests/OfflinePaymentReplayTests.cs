using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflinePaymentReplayTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Ctx(long Branch, long Warehouse, long Business, long AdminId);

    private async Task<Ctx> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var ctx = new Ctx(branch, warehouse, business, admin);
        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        return ctx;
    }

    private async Task<long> CreateDebtorAsync(decimal openingDebt)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCustomerCommand("Oflayn Qarzdor",
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: 10_000_000m, OpeningBalance: openingDebt));
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == AccountType.Debt))?.Balance ?? 0m;
    }

    private static OfflineSyncEventRequest CashPayment(long sequence, long customerId, long branchId, decimal amount,
        List<CustomerPaymentAllocationRequest>? allocations = null, bool autoAllocateDebt = true,
        long? actorUserId = null, string method = "Cash") =>
        TestOffline.Event(sequence, "customer.payment.create", new CreateCustomerPaymentRequest(customerId, branchId,
            [new CustomerPaymentTenderRequest(method, "UZS", amount)], allocations, autoAllocateDebt), actorUserId);

    [Fact]
    public async Task OFF_21_Excess_over_current_debt_becomes_advance()
    {
        var ctx = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateDebtorAsync(100_000m);
        Assert.Equal(100_000m, await DebtAsync(customerId));

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateCustomerPaymentCommand(customerId, ctx.Branch,
                [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", 40_000m)]));
        }
        Assert.Equal(60_000m, await DebtAsync(customerId));

        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        var result = await TestOffline.PushAsync(Fixture, grant, CashPayment(1, customerId, ctx.Branch, 100_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = await db.CustomerPaymentDocuments.AsNoTracking().SingleAsync(d => d.Id == applied.ResultEntityId);
        Assert.Equal(100_000m, doc.TotalBaseAmount);
        Assert.Equal(60_000m, doc.AllocatedBaseAmount);
        Assert.Equal(40_000m, doc.AdvanceBaseAmount);
        Assert.Equal(0m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task OFF_20_Explicit_allocations_are_rejected()
    {
        var ctx = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateDebtorAsync(50_000m);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant, CashPayment(1, customerId, ctx.Branch, 10_000m,
            allocations: [new CustomerPaymentAllocationRequest("UZS", 10_000m)]));

        var rejected = Assert.Single(result.Results);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("offline_payment_allocations_unsupported", rejected.ErrorCode);
        Assert.Equal(50_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task OFF_20_Disabled_auto_allocation_is_rejected()
    {
        var ctx = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateDebtorAsync(50_000m);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            CashPayment(1, customerId, ctx.Branch, 10_000m, autoAllocateDebt: false));

        var rejected = Assert.Single(result.Results);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("offline_payment_allocations_unsupported", rejected.ErrorCode);
    }

    [Fact]
    public async Task OFF_22_Payment_is_authored_by_the_offline_actor()
    {
        var ctx = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateDebtorAsync(50_000m);
        var actorId = await TestOffline.CreateActorAsync(Fixture, ctx.Branch,
            "customer_payments.create", "customers.receivePayment");
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        // OFF-05: naqd amal muallifning ochiq smenasini talab qiladi — mualliflikni
        // smenasiz tekshirish uchun karta tenderi ishlatiladi.
        var result = await TestOffline.PushAsync(Fixture, grant,
            CashPayment(1, customerId, ctx.Branch, 50_000m, actorUserId: actorId, method: "Card"));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = await db.CustomerPaymentDocuments.AsNoTracking().SingleAsync(d => d.Id == applied.ResultEntityId);
        Assert.Equal(actorId, doc.UserId);
        Assert.NotEqual(ctx.AdminId, doc.UserId);
    }

    [Fact]
    public async Task OFF_22_Actor_without_payment_permission_is_rejected()
    {
        var ctx = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateDebtorAsync(50_000m);
        var actorId = await TestOffline.CreateActorAsync(Fixture, ctx.Branch);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            CashPayment(1, customerId, ctx.Branch, 50_000m, actorUserId: actorId));

        Assert.Equal("Rejected", Assert.Single(result.Results).Status);
        Assert.Equal(50_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task OFF_05_Cash_payment_replay_requires_open_shift()
    {
        var ctx = await SetupAsync();
        var customerId = await CreateDebtorAsync(30_000m);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        var payment = CashPayment(1, customerId, ctx.Branch, 30_000m);

        var closed = await TestOffline.PushAsync(Fixture, grant, payment);
        Assert.Equal("Rejected", Assert.Single(closed.Results).Status);
        Assert.Equal(30_000m, await DebtAsync(customerId));

        await TestShift.OpenAsync(Fixture);
        var retried = await TestOffline.PushAsync(Fixture, grant, payment);
        Assert.Equal("Applied", Assert.Single(retried.Results).Status);
        Assert.Equal(0m, await DebtAsync(customerId));
    }
}
