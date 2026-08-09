using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class CustomerCreditTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync(bool multicurrency = false)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (multicurrency)
            await db.Features.Where(f => f.Code == FeatureCatalog.Multicurrency
                                         || f.Code == FeatureCatalog.PricingMulticurrency
                                         || f.Code == FeatureCatalog.SalesMulticurrency)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, true));
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    private async Task SetPolicyAsync(bool allowCustomerCredit = true, bool allowDebtSales = true)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy,
            new SalesPolicySettings { AllowCustomerCredit = allowCustomerCredit, AllowDebtSales = allowDebtSales });
    }

    private async Task<long> CreateCustomerAsync(decimal openingBalance = 0)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateCustomerCommand("Haqdor Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            OpeningBalance: openingBalance));
    }

    private async Task<decimal> DebtBalanceAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt);
        return account?.Balance ?? 0m;
    }

    private async Task<decimal> AdvanceBalanceAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == customerId && a.Type == AccountType.CustomerAdvance);
        return account?.Balance ?? 0m;
    }

    private async Task<decimal> BranchBalanceAsync(long branch, AccountType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.BranchId == branch && a.Type == type);
        return account?.Balance ?? 0m;
    }

    private async Task<int> TxCountAsync(long saleId, OperationType type)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Transactions.CountAsync(t => t.SaleId == saleId && t.OperationType == type);
    }

    private static void AssertInvariant(Cartex.Domain.Entities.Sale sale) =>
        Assert.Equal(sale.TotalAmount + sale.CreditAmount,
            sale.PaidCash + sale.PaidCard + sale.PaidBonus + sale.PaidAdvance + sale.DebtAmount);

    [Fact]
    public async Task Overpay_converted_to_credit_keeps_cash_and_posts_customer_advance()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;
        var cashBefore = await BranchBalanceAsync(branch1, AccountType.Cash);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, total + 20_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 20_000m))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);
        var credit = await db.Transactions.SingleAsync(t =>
            t.SaleId == saleId && t.OperationType == OperationType.CustomerAdvance);

        Assert.Equal(total, sale.TotalAmount);
        Assert.Equal(20_000m, sale.CreditAmount);
        Assert.Equal(0m, sale.ChangeAmount);
        Assert.Equal(total + 20_000m, sale.PaidCash);
        Assert.Equal(cashBefore + total + 20_000m, await BranchBalanceAsync(branch1, AccountType.Cash));
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(20_000m, await AdvanceBalanceAsync(customerId));
        Assert.Equal(20_000m, credit.Amount);
        Assert.Equal(0, await TxCountAsync(saleId, OperationType.Change));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Partial_credit_gives_remaining_excess_as_change()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, total + 30_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(20_000m, sale.ChangeAmount);
        Assert.Equal(10_000m, sale.CreditAmount);
        Assert.Equal(total + 10_000m, sale.PaidCash);
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(10_000m, await AdvanceBalanceAsync(customerId));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Credit_above_excess_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, customerId, price * 2 + 5000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m)));
    }

    [Fact]
    public async Task Credit_without_customer_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, null, price * 2 + 10_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m)));
    }

    [Fact]
    public async Task Credit_throws_when_policy_disabled()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync(allowCustomerCredit: false);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, customerId, price * 2 + 10_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m)));
    }

    [Fact]
    public async Task Policy_disabled_keeps_plain_overpay_change_behaviour()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync(allowCustomerCredit: false);
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;
        var cashBefore = await BranchBalanceAsync(branch1, AccountType.Cash);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, total + 5000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)]))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(5000m, sale.ChangeAmount);
        Assert.Equal(0m, sale.CreditAmount);
        Assert.Equal(total, sale.PaidCash);
        Assert.Equal(cashBefore + total, await BranchBalanceAsync(branch1, AccountType.Cash));
        Assert.Equal(0, await TxCountAsync(saleId, OperationType.CustomerAdvance));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Card_excess_fully_credited_is_allowed()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;
        var cardBefore = await BranchBalanceAsync(branch1, AccountType.Card);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, total + 10_000m, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(0m, sale.ChangeAmount);
        Assert.Equal(10_000m, sale.CreditAmount);
        Assert.Equal(0m, sale.PaidCash);
        Assert.Equal(total + 10_000m, sale.PaidCard);
        Assert.Equal(cardBefore + total + 10_000m, await BranchBalanceAsync(branch1, AccountType.Card));
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(10_000m, await AdvanceBalanceAsync(customerId));
        Assert.Equal(1, await TxCountAsync(saleId, OperationType.CustomerAdvance));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Card_excess_with_leftover_change_throws()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, price * 2 + 10_000m, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 5000m)));
    }

    [Fact]
    public async Task Bonus_excess_cannot_become_credit()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new GiveCustomerBonusCommand(customerId, total + 50_000m, "test"));
        }

        using var check = Fixture.CreateScope();
        var sender2 = check.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender2.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, total + 10_000m,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 10_000m)));
    }

    [Fact]
    public async Task Debt_sale_uses_existing_advance_before_creating_debt()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync(openingBalance: -20_000m);
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(20_000m, await AdvanceBalanceAsync(customerId));

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0,
                [new CreateSaleItemDto(variantId, 1, 50_000m)]))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(20_000m, sale.PaidAdvance);
        Assert.Equal(30_000m, sale.DebtAmount);
        Assert.Equal(30_000m, await DebtBalanceAsync(customerId));
        Assert.Equal(0m, await AdvanceBalanceAsync(customerId));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Debt_sales_disabled_blocks_plain_debt_but_allows_credit_covered_debt()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync(allowDebtSales: false);
        await TestShift.OpenAsync(Fixture);

        var plainId = await CreateCustomerAsync();
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, plainId, 0, 0, 0,
                    [new CreateSaleItemDto(variantId, 1, 30_000m)])));
        }

        var creditorId = await CreateCustomerAsync(openingBalance: -50_000m);
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, creditorId, 0, 0, 0,
                [new CreateSaleItemDto(variantId, 1, 30_000m)]))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(30_000m, sale.PaidAdvance);
        Assert.Equal(0m, sale.DebtAmount);
        Assert.Equal(0m, await DebtBalanceAsync(creditorId));
        Assert.Equal(20_000m, await AdvanceBalanceAsync(creditorId));
        AssertInvariant(sale);
    }

    [Fact]
    public async Task Return_of_credit_sale_refunds_total_and_keeps_credit()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;
        var cashBefore = await BranchBalanceAsync(branch1, AccountType.Cash);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, total + 20_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)], CreditAmount: 20_000m))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lines = await db.SaleItems.Where(i => i.SaleId == saleId)
                .Select(i => new ReturnLineDto(i.Id, i.Quantity, true, null)).ToListAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ReturnSaleCommand(saleId, lines));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db2.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(SaleStatus.Returned, sale.Status);
        Assert.Equal(sale.TotalAmount, sale.RefundedCash);
        Assert.Equal(20_000m, sale.CreditAmount);
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(20_000m, await AdvanceBalanceAsync(customerId));
        Assert.Equal(cashBefore + 20_000m, await BranchBalanceAsync(branch1, AccountType.Cash));
        Assert.Equal(1, await TxCountAsync(saleId, OperationType.CustomerAdvance));
    }

    [Fact]
    public async Task Payments_row_overpay_converts_to_credit_without_change_posting()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync(multicurrency: true);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetPolicyAsync();
        await TestShift.OpenAsync(Fixture);

        var customerId = await CreateCustomerAsync();
        var total = price * 2;

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0,
                [new CreateSaleItemDto(variantId, 2)],
                Payments: [new SalePaymentDto(PaymentMethod.Cash, "UZS", total + 20_000m)],
                CreditAmount: 20_000m))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.Id == saleId);

        Assert.Equal(0m, sale.ChangeAmount);
        Assert.Equal(20_000m, sale.CreditAmount);
        Assert.Equal(total + 20_000m, sale.PaidCash);
        Assert.Equal(1, await TxCountAsync(saleId, OperationType.CustomerAdvance));
        Assert.Equal(0, await TxCountAsync(saleId, OperationType.Change));
        Assert.Equal(0m, await DebtBalanceAsync(customerId));
        Assert.Equal(20_000m, await AdvanceBalanceAsync(customerId));
        AssertInvariant(sale);
    }
}
