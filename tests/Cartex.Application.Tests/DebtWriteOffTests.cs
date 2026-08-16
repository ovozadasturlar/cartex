using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Participants;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Partners.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class DebtWriteOffTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal SaleTotal = 1_000_000m;
    private const decimal Paid = 900_000m;
    private const decimal Forgiven = SaleTotal - Paid;  // 1 000 000 - 900 000 = 100 000
    private const decimal SmallTotal = 400_000m;
    private const string Reason = "Kelishuv bo'yicha kechirildi";

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long BigVariant, long SmallVariant);

    private sealed record SaleSnapshot(
        decimal Total, decimal Discount, decimal Rounding, decimal Debt, decimal Cashback, SaleStatus Status);

    /// Two stocked variants repriced to 1 000 000 and 400 000 in the base currency, so every
    /// number in the assertions comes straight from the acceptance criterion.
    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = await db.Businesses.FirstAsync();
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 2).Select(s => s.VariantId);
        var variants = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .Distinct()
            .OrderBy(x => x)
            .Take(2)
            .ToListAsync();
        Assert.Equal(2, variants.Count);

        foreach (var price in await db.ProductPrices.Where(p => variants.Contains(p.VariantId)).ToListAsync())
        {
            price.SellingPrice = price.VariantId == variants[0] ? SaleTotal : SmallTotal;
            price.Currency = business.Currency;
        }
        await db.SaveChangesAsync();

        return new Setup(branch, warehouse, business.Id, admin, variants[0], variants[1]);
    }

    private async Task AsAdminAsync(Setup s)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
    }

    private async Task SetPolicyAsync(decimal maxWriteOffAmount, decimal maxWriteOffPercent)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
        {
            MaxDiscountPercent = 100,
            MaxDebtWriteOffAmount = maxWriteOffAmount,
            MaxDebtWriteOffPercent = maxWriteOffPercent
        });
    }

    private async Task<T> Send<T>(Func<ISender, Task<T>> action)
    {
        using var scope = Fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    private Task<long> CreateCustomerAsync() =>
        Send(s => s.Send(new CreateCustomerCommand("Kechirim Mijoz",
            "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 100_000_000m)));

    private Task<long> SellOnDebtAsync(Setup s, long variantId, long customerId,
        DateOnly? due = null, List<ParticipantInput>? participants = null) =>
        Send(async x => (await x.Send(new CreateSaleCommand(
            s.Warehouse, customerId, 0, 0, 0,
            [new CreateSaleItemDto(variantId, 1m)],
            DebtDueDate: due,
            ApplyAutoDiscount: false,
            UseCustomerAdvance: false,
            Participants: participants))).SaleId);

    private Task<CustomerPaymentCreatedDto> PayAsync(Setup s, long customerId, decimal cash, decimal writeOff,
        string? reason = Reason)
    {
        List<CustomerPaymentTenderInput> tenders = cash > 0
            ? [new CustomerPaymentTenderInput(PaymentMethod.Cash, "UZS", cash)]
            : [];
        return Send(x => x.Send(new CreateCustomerPaymentCommand(customerId, s.Branch, tenders,
            WriteOffAmount: writeOff,
            WriteOffReason: reason,
            IdempotencyKey: $"write-off-{Guid.NewGuid():N}")));
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(a => a.CustomerId == customerId && a.Type == AccountType.Debt)
            .SumAsync(a => a.Balance);
    }

    private async Task<decimal> CashAsync(long branchId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(a => a.BranchId == branchId && a.Type == AccountType.Cash)
            .SumAsync(a => a.Balance);
    }

    private async Task<decimal> PostedAsync(long documentId, OperationType operation)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Transactions
            .Where(t => t.CustomerPaymentDocumentId == documentId && t.OperationType == operation)
            .SumAsync(t => t.Amount);
    }

    private async Task<decimal> RewardAsync(long partnerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.PartnerRewardEntries.Where(e => e.PartnerProfileId == partnerId).SumAsync(e => e.Amount);
    }

    private async Task<SaleSnapshot> SnapshotAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Sales.Where(x => x.Id == saleId)
            .Select(x => new SaleSnapshot(
                x.TotalAmount, x.DiscountAmount, x.RoundingAmount, x.DebtAmount, x.CashbackEarned, x.Status))
            .SingleAsync();
    }

    [Fact]
    public async Task QARZ_12_Payment_and_write_off_in_one_document_close_the_debt()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);
        // 1 x 1 000 000 taken on credit, nothing tendered -> debt 1 000 000
        Assert.Equal(SaleTotal, await DebtAsync(customerId));

        var payment = await PayAsync(s, customerId, Paid, Forgiven);

        // 1 000 000 - 900 000 paid - 100 000 forgiven = 0
        Assert.Equal(0m, await DebtAsync(customerId));

        // the ledger keeps the two events apart: money taken vs money given up
        Assert.Equal(Paid, await PostedAsync(payment.Id, OperationType.DebtPay));
        Assert.Equal(Forgiven, await PostedAsync(payment.Id, OperationType.DebtWriteOff));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await db.CustomerPaymentDocuments.SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(Forgiven, document.WriteOffBaseAmount);
        Assert.Equal(Reason, document.WriteOffReason);
    }

    [Fact]
    public async Task QARZ_04_Write_off_does_not_touch_the_closed_sale()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        var saleId = await SellOnDebtAsync(s, s.BigVariant, customerId);
        var before = await SnapshotAsync(saleId);

        // 1 x 1 000 000 at the catalog price, no discount rule applied -> total 1 000 000, discount 0
        Assert.Equal(SaleTotal, before.Total);
        Assert.Equal(0m, before.Discount);
        Assert.Equal(SaleTotal, before.Debt);

        await PayAsync(s, customerId, Paid, Forgiven);

        // forgiveness is a new event today, not a retroactive edit of a sale that already
        // drove cashback and partner rewards inside a closed shift
        Assert.Equal(before, await SnapshotAsync(saleId));
        Assert.Equal(0m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task QARZ_05_Partner_reward_follows_only_the_money_actually_paid()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        long partnerId;
        long roleId;
        long partyId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            partnerId = await sender.Send(new CreatePartnerCommand("Kechirim hamkori", CustomerId: customerId));
            roleId = await sender.Send(new SaveParticipantRoleCommand(null, "writeoff_referrer", "Hamkor", "Hamkorlar"));
            await sender.Send(new SavePartnerProgramCommand(null, roleId, "To'langanda 10%", true,
                PartnerRewardMode.Points, PartnerRewardBasis.NetRevenue, PartnerRewardTrigger.Payment, 10m));
            partyId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PartnerProfiles
                .Where(x => x.Id == partnerId).Select(x => x.PartyId).SingleAsync();
        }

        await SellOnDebtAsync(s, s.BigVariant, customerId, participants: [new ParticipantInput(roleId, partyId)]);
        // the program pays on settlement, so the credit sale alone earns nothing
        Assert.Equal(0m, await RewardAsync(partnerId));

        await PayAsync(s, customerId, Paid, Forgiven);

        // 900 000 x 10% = 90 000. The forgiven 100 000 is not money received, so it adds
        // nothing - a reward on the full 1 000 000 would have been 100 000.
        Assert.Equal(Math.Round(Paid * .10m, 4), await RewardAsync(partnerId));
    }

    [Fact]
    public async Task QARZ_06_Write_off_over_the_policy_amount_is_refused()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        // the owner caps forgiveness at 50 000; the requested 100 000 is over it
        await SetPolicyAsync(50_000m, 100m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => PayAsync(s, customerId, Paid, Forgiven));

        // the whole document is refused, the 900 000 included: debt is still 1 000 000
        Assert.Equal(SaleTotal, await DebtAsync(customerId));
    }

    [Fact]
    public async Task QARZ_06_Write_off_over_the_policy_percent_is_refused()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        // amount cap wide open, percent cap 5%. 100 000 is 10% of the 1 000 000 sale, 10% of the
        // 1 000 000 debt and 10% of the 1 000 000 document - over 5% on every reading of the base.
        await SetPolicyAsync(SaleTotal, 5m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => PayAsync(s, customerId, Paid, Forgiven));
        Assert.Equal(SaleTotal, await DebtAsync(customerId));
    }

    [Fact]
    public async Task QARZ_06_Write_off_without_the_permission_is_forbidden()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);

        // a cashier who may take money but may not give any of it up
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        Fixture.CurrentUser.Granted.Add(AppPermissions.CustomerPayments.Create);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.ReceivePayment);
        // Setup only: the customer belongs to the admin, so without this the cashier cannot
        // even see it and every call fails as not-found before reaching the guard under test.
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.ViewAll);

        await Assert.ThrowsAsync<ForbiddenException>(() => PayAsync(s, customerId, Paid, Forgiven));
        Assert.Equal(SaleTotal, await DebtAsync(customerId));

        // the guard covers the forgiveness only - a plain 900 000 payment still goes through
        await PayAsync(s, customerId, Paid, 0m, reason: null);
        // 1 000 000 - 900 000 = 100 000
        Assert.Equal(Forgiven, await DebtAsync(customerId));

        // and that one permission is the whole gate: granted, the same forgiveness is accepted
        Fixture.CurrentUser.Granted.Add(AppPermissions.CustomerPayments.WriteOffDebt);
        await PayAsync(s, customerId, 0m, Forgiven);
        Assert.Equal(0m, await DebtAsync(customerId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task QARZ_06_Write_off_without_a_reason_is_refused(string? reason)
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => PayAsync(s, customerId, Paid, Forgiven, reason));
        Assert.True(error is ValidationException or BusinessRuleException, error.GetType().Name);
        Assert.Equal(SaleTotal, await DebtAsync(customerId));
    }

    [Fact]
    public async Task QARZ_12_Forgiveness_without_any_cash_closes_the_sale()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        var saleId = await SellOnDebtAsync(s, s.BigVariant, customerId);
        var cashBefore = await CashAsync(s.Branch);

        var payment = await PayAsync(s, customerId, 0m, SaleTotal);

        // the whole 1 000 000 is given up and nothing is collected
        Assert.Equal(0m, await DebtAsync(customerId));
        Assert.Equal(SaleTotal, await PostedAsync(payment.Id, OperationType.DebtWriteOff));
        Assert.Equal(0m, await PostedAsync(payment.Id, OperationType.DebtPay));
        Assert.Equal(cashBefore, await CashAsync(s.Branch));

        // allocated onto the sale exactly like a payment, so the sale really closes
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(SaleTotal, await db.CustomerPaymentAllocations
            .Where(x => x.CustomerPaymentDocumentId == payment.Id && x.SaleId == saleId)
            .SumAsync(x => x.AmountBase));
    }

    [Fact]
    public async Task QARZ_12_Forgiveness_starts_from_the_oldest_due_debt()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var customerId = await CreateCustomerAsync();
        var dueSoon = await SellOnDebtAsync(s, s.SmallVariant, customerId, today.AddDays(7));   // 400 000
        var dueLater = await SellOnDebtAsync(s, s.BigVariant, customerId, today.AddDays(60));   // 1 000 000

        var payment = await PayAsync(s, customerId, 0m, Forgiven);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var allocations = await db.CustomerPaymentAllocations
            .Where(x => x.CustomerPaymentDocumentId == payment.Id)
            .ToListAsync();

        // 100 000 forgiven, nearest due date first: all of it lands on the 400 000 sale
        Assert.Equal(Forgiven, allocations.Where(x => x.SaleId == dueSoon).Sum(x => x.AmountBase));
        Assert.Equal(0m, allocations.Where(x => x.SaleId == dueLater).Sum(x => x.AmountBase));
        // 400 000 + 1 000 000 - 100 000 = 1 300 000
        Assert.Equal(1_300_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task QARZ_13_Voiding_the_document_restores_the_forgiven_debt()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        await SetPolicyAsync(SaleTotal, 100m);

        var customerId = await CreateCustomerAsync();
        await SellOnDebtAsync(s, s.BigVariant, customerId);
        var payment = await PayAsync(s, customerId, Paid, Forgiven);
        Assert.Equal(0m, await DebtAsync(customerId));

        await Send(x => x.Send(new VoidCustomerPaymentCommand(payment.Id, "Kechirim noto'g'ri berildi")));

        // 900 000 taken back and the 100 000 un-forgiven -> the original 1 000 000
        Assert.Equal(SaleTotal, await DebtAsync(customerId));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(BusinessDocumentStatus.Voided, await db.CustomerPaymentDocuments
            .Where(x => x.Id == payment.Id).Select(x => x.Status).SingleAsync());
    }
}
