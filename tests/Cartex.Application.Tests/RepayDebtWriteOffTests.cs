using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class RepayDebtWriteOffTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Debt = 100_000m;
    private const decimal Paid = 60_000m;
    private const decimal Forgiven = 30_000m;
    private const string Reason = "sinov";

    private async Task<long> SetupCustomerWithDebtAsync()
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
        await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 1, Debt)]));
        return customerId;
    }

    private async Task SetPolicyAsync(decimal maxWriteOffAmount)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
        {
            MaxDiscountPercent = 100,
            MaxDebtWriteOffAmount = maxWriteOffAmount,
            MaxDebtWriteOffPercent = 100m
        });
    }

    private async Task RepayAsync(long customerId, decimal amount, decimal writeOff, string? reason)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new RepayCustomerDebtCommand(customerId, amount, ViaCard: true, WriteOff: writeOff, WriteOffReason: reason));
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == AccountType.Debt))?.Balance ?? 0m;
    }

    private async Task<decimal> WriteOffPostedAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Transactions
            .Where(t => t.OperationType == OperationType.DebtWriteOff &&
                (t.FromAccount!.CustomerId == customerId ||
                 t.ToAccount!.CustomerId == customerId ||
                 t.CustomerPaymentDocument!.CustomerId == customerId))
            .SumAsync(t => t.Amount);
    }

    // QARZ-12
    [Fact]
    public async Task QARZ_12_repay_with_writeoff_reduces_debt()
    {
        var customerId = await SetupCustomerWithDebtAsync();
        await SetPolicyAsync(Debt);

        await RepayAsync(customerId, Paid, Forgiven, Reason);

        Assert.Equal(Debt - Paid - Forgiven, await DebtAsync(customerId));
        Assert.Equal(Forgiven, await WriteOffPostedAsync(customerId));
    }

    // QARZ-06
    [Fact]
    public async Task QARZ_06_writeoff_without_reason_rejected()
    {
        var customerId = await SetupCustomerWithDebtAsync();
        await SetPolicyAsync(Debt);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => RepayAsync(customerId, Paid, Forgiven, null));
        Assert.True(error is ValidationException or BusinessRuleException, error.GetType().Name);
        Assert.Equal(Debt, await DebtAsync(customerId));
    }

    // QARZ-06
    [Fact]
    public async Task QARZ_06_writeoff_over_amount_limit_rejected()
    {
        var customerId = await SetupCustomerWithDebtAsync();
        await SetPolicyAsync(10_000m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RepayAsync(customerId, Paid, Forgiven, Reason));
        Assert.Equal(Debt, await DebtAsync(customerId));
    }

    // QARZ-06
    [Fact]
    public async Task QARZ_06_partial_repay_never_auto_writes_off()
    {
        var customerId = await SetupCustomerWithDebtAsync();
        await SetPolicyAsync(Debt);

        await RepayAsync(customerId, Paid, 0m, null);

        Assert.Equal(Debt - Paid, await DebtAsync(customerId));
        Assert.Equal(0m, await WriteOffPostedAsync(customerId));
    }

    // QARZ-16
    [Fact]
    public async Task QARZ_16_writeoff_only_without_payment()
    {
        var customerId = await SetupCustomerWithDebtAsync();
        await SetPolicyAsync(Debt);

        await RepayAsync(customerId, 0m, Forgiven, Reason);

        Assert.Equal(Debt - Forgiven, await DebtAsync(customerId));
        Assert.Equal(Forgiven, await WriteOffPostedAsync(customerId));
    }
}
