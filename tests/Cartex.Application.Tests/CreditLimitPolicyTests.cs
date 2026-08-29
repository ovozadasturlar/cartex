using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Shifts.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// QARZ-22: kredit limitining qattiqligi savdo siyosatidan boshqariladi. Testlar hujjatdagi
/// qabul mezonidan yozilgan — ikkinchi qarz savdosi limitni kesib o'tadi va rejimga qarab
/// rad etiladi yoki ogohlantirish bilan o'tadi.
[Collection("database")]
public class CreditLimitPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const string Warning = "credit_limit_exceeded";

    private sealed record Ctx(long Branch, long Warehouse, long Business, long Admin, long VariantId, decimal UnitPrice);

    private async Task<Ctx> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor");
        var variant = await db.ProductVariants.FirstAsync(v => v.ProductId == product.Id);
        var price = await db.ProductPrices
            .Where(p => p.VariantId == variant.Id)
            .OrderBy(p => p.WarehouseId == null ? 1 : 0)
            .FirstAsync();
        return new Ctx(
            (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id,
            (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id,
            (await db.Businesses.FirstAsync()).Id,
            (await db.Users.FirstAsync(u => u.Username == "admin")).Id,
            variant.Id,
            price.SellingPrice);
    }

    private async Task SignInAsync(Ctx ctx)
    {
        Fixture.CurrentUser.AsAdmin(ctx.Admin, ctx.Business, ctx.Branch);
        await TestShift.OpenAsync(Fixture);
    }

    private async Task SetPolicyAsync(string enforcement, bool allowDebtSales = true, bool allowCustomerLoans = false,
        decimal? defaultCreditLimit = null)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
            {
                CreditLimitEnforcement = enforcement,
                AllowDebtSales = allowDebtSales,
                AllowCustomerLoans = allowCustomerLoans,
                DefaultCreditLimit = defaultCreditLimit
            });
    }

    private async Task<decimal?> StoredLimitAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Customers.FirstAsync(c => c.Id == customerId)).CreditLimit;
    }

    private Task<long> CreateCustomerAsync(decimal? creditLimit) =>
        Send(x => x.Send(new CreateCustomerCommand(
            "Limit Mijozi", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: creditLimit)));

    private Task<CreateSaleResult> DebtSaleAsync(Ctx ctx, long customerId, decimal qty = 1m, bool offlineReplay = false) =>
        Send(x => x.Send(new CreateSaleCommand(ctx.Warehouse, customerId, 0, 0, 0,
            [new CreateSaleItemDto(ctx.VariantId, qty)])
        {
            FromOfflineSync = offlineReplay
        }));

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == AccountType.Debt))?.Balance ?? 0m;
    }

    private async Task<T> Send<T>(Func<ISender, Task<T>> action)
    {
        using var scope = Fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISender>());
    }

    // QARZ-22: Block (standart) — limitdan oshiradigan qarz rad etiladi, qarz o'zgarmaydi.
    [Fact]
    public async Task Block_refuses_the_sale_that_crosses_the_limit_and_leaves_the_debt_untouched()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block");

        var customerId = await CreateCustomerAsync(ctx.UnitPrice * 1.5m);
        await DebtSaleAsync(ctx, customerId);
        var debtAfterFirst = await DebtAsync(customerId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DebtSaleAsync(ctx, customerId));

        Assert.Equal(Warning, error.Code);
        Assert.Equal(debtAfterFirst, await DebtAsync(customerId));
    }

    // QARZ-22: Warn — savdo o'tadi, qarz oshadi va javobda ogohlantirish qaytadi.
    [Fact]
    public async Task Warn_lets_the_sale_through_and_returns_the_warning()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Warn");

        var customerId = await CreateCustomerAsync(ctx.UnitPrice * 1.5m);
        await DebtSaleAsync(ctx, customerId);
        var debtAfterFirst = await DebtAsync(customerId);

        var result = await DebtSaleAsync(ctx, customerId);

        Assert.Contains(Warning, result.Warnings ?? []);
        Assert.Equal(debtAfterFirst * 2, await DebtAsync(customerId));
    }

    // QARZ-22: limit ichidagi savdo ogohlantirish qoldirmaydi.
    [Fact]
    public async Task A_sale_inside_the_limit_carries_no_warning()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Warn");

        var customerId = await CreateCustomerAsync(ctx.UnitPrice * 10m);
        var result = await DebtSaleAsync(ctx, customerId);

        Assert.True(result.Warnings is null or { Count: 0 });
    }

    // QARZ-22: `Warn` taqiqni ochmaydi — nol limit har ikki rejimda ham rad etadi.
    [Fact]
    public async Task Warn_does_not_open_a_zero_credit_limit()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Warn");

        var customerId = await CreateCustomerAsync(0m);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => DebtSaleAsync(ctx, customerId));

        Assert.Equal(Warning, error.Code);
        Assert.Equal(0m, await DebtAsync(customerId));
    }

    // QARZ-22: `Warn` `AllowDebtSales` taqiqini ham ochmaydi.
    [Fact]
    public async Task Warn_does_not_open_disabled_debt_sales()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Warn", allowDebtSales: false);

        var customerId = await CreateCustomerAsync(ctx.UnitPrice * 10m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => DebtSaleAsync(ctx, customerId));
        Assert.Equal(0m, await DebtAsync(customerId));
    }

    // QARZ-22: limit bo'sh bo'lsa tekshiruv umuman ishlamaydi.
    [Fact]
    public async Task An_empty_limit_never_warns_however_large_the_debt_grows()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block");

        var customerId = await CreateCustomerAsync(null);
        var result = await DebtSaleAsync(ctx, customerId, qty: 5m);

        Assert.True(result.Warnings is null or { Count: 0 });
        Assert.True(await DebtAsync(customerId) > 0m);
    }

    // OFF-22: replay `Block` rejimida ham rad etmaydi — hodisa allaqachon sodir bo'lgan.
    [Fact]
    public async Task Offline_replay_never_refuses_a_sale_that_crosses_the_limit()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block");

        var customerId = await CreateCustomerAsync(ctx.UnitPrice * 1.5m);
        await DebtSaleAsync(ctx, customerId);
        var debtAfterFirst = await DebtAsync(customerId);

        var result = await DebtSaleAsync(ctx, customerId, offlineReplay: true);

        Assert.Contains(Warning, result.Warnings ?? []);
        Assert.Equal(debtAfterFirst * 2, await DebtAsync(customerId));
    }

    // QARZ-18 + QARZ-22: naqd qarz ham xuddi shu sozlamaga bo'ysunadi.
    [Fact]
    public async Task Block_refuses_a_cash_loan_that_crosses_the_limit()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block", allowCustomerLoans: true);
        await Send(x => x.Send(new AddCashMovementCommand(20_000_000m, IsPayOut: false)));

        var customerId = await CreateCustomerAsync(100_000m);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Send(x => x.Send(
            new CreateCustomerRefundCommand(customerId, ctx.Branch,
                [new CustomerRefundTenderInput(PaymentMethod.Cash, "UZS", 500_000m)]))));

        Assert.Equal(Warning, error.Code);
    }

    // QARZ-18 + QARZ-22: `Warn` rejimida naqd qarz ham o'tadi.
    [Fact]
    public async Task Warn_lets_a_cash_loan_through_over_the_limit()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Warn", allowCustomerLoans: true);
        await Send(x => x.Send(new AddCashMovementCommand(20_000_000m, IsPayOut: false)));

        var customerId = await CreateCustomerAsync(100_000m);

        var result = await Send(x => x.Send(new CreateCustomerRefundCommand(customerId, ctx.Branch,
            [new CustomerRefundTenderInput(PaymentMethod.Cash, "UZS", 500_000m)])));

        Assert.Contains(Warning, result.Warnings ?? []);
        Assert.Equal(500_000m, await DebtAsync(customerId));
    }

    // SOZ-17: do'kon standarti faqat klient formasi uchun — server uni hech qachon o'zi qo'llamaydi.
    [Fact]
    public async Task An_omitted_limit_stays_unlimited_even_when_the_shop_sets_a_default()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block", defaultCreditLimit: 100_000m);

        var customerId = await CreateCustomerAsync(null);

        Assert.Null(await StoredLimitAsync(customerId));
    }

    // SOZ-17 + SOZ-02a: bo'sh limit cheklanmagan degani — u savdoni to'xtatmaydi.
    [Fact]
    public async Task An_omitted_limit_does_not_block_a_debt_sale()
    {
        var ctx = await SetupAsync();
        await SignInAsync(ctx);
        await SetPolicyAsync("Block", defaultCreditLimit: 1m);

        var customerId = await CreateCustomerAsync(null);
        var result = await DebtSaleAsync(ctx, customerId, qty: 3m);

        Assert.True(result.Warnings is null or { Count: 0 });
        Assert.True(await DebtAsync(customerId) > 0m);
    }
}
