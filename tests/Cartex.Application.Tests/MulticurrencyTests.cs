using Cartex.Application.Customers.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Rates.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Suppliers.Commands;
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
public class MulticurrencyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId, decimal price)> SetupAsync(bool enableFeature = true)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (enableFeature)
            await db.Features.Where(f => f.Code == FeatureCatalog.Multicurrency)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, true));
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Coca-Cola 1.5L")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        var price = await db.ProductPrices.Where(p => p.VariantId == variantId).Select(p => p.SellingPrice).FirstAsync();
        return (branch1, warehouse1, businessId, adminId, variantId, price);
    }

    private async Task SetRateAsync(string code, decimal rate)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new SetExchangeRateCommand(code, rate));
    }

    private async Task<decimal> BalanceAsync(Func<Cartex.Domain.Entities.Account, bool> predicate)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var accounts = await db.Accounts.ToListAsync();
        return accounts.Where(predicate).Sum(a => a.Balance);
    }

    [Fact]
    public async Task Foreign_payment_rejected_when_feature_disabled()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync(enableFeature: false);
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12600m);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateSaleCommand(warehouse1, null, 0, 0, 0, [new CreateSaleItemDto(variantId, 1)],
                Payments: [new SalePaymentDto(PaymentMethod.Cash, "USD", 10m)])));
    }

    [Fact]
    public async Task Mixed_usd_and_base_tender_posts_to_separate_cash_accounts()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12600m);

        var total = price * 2;
        var usdAmount = 1m;
        var usdBase = Math.Round(usdAmount * 12600m, 2);
        var uzsAmount = total - usdBase;

        string token;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            token = (await sender.Send(new CreateSaleCommand(warehouse1, null, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)],
                Payments:
                [
                    new SalePaymentDto(PaymentMethod.Cash, "USD", usdAmount),
                    new SalePaymentDto(PaymentMethod.Cash, "UZS", uzsAmount)
                ]))).ReceiptToken;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(s => s.Payments).FirstAsync(s => s.ReceiptToken == token);
        var usdCash = await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash && a.Currency == "USD");
        var uzsCash = await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash && a.Currency == "UZS");

        Assert.Equal(2, sale.Payments.Count);
        Assert.Equal(total, sale.PaidCash);
        Assert.Equal(0m, sale.DebtAmount);
        Assert.Equal(usdAmount, usdCash.Balance);
        Assert.Equal(uzsAmount, uzsCash.Balance);
        Assert.Equal(sale.TotalAmount, sale.PaidCash + sale.PaidCard + sale.PaidBonus + sale.DebtAmount);
    }

    [Fact]
    public async Task Usd_only_tender_gives_change_from_base_cash()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12600m);

        var total = price * 2;
        var usdAmount = Math.Ceiling(total / 12600m) + 1;
        var usdBase = Math.Round(usdAmount * 12600m, 2);
        var expectedChange = usdBase - total;

        string token;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            token = (await sender.Send(new CreateSaleCommand(warehouse1, null, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)],
                Payments: [new SalePaymentDto(PaymentMethod.Cash, "USD", usdAmount)]))).ReceiptToken;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.FirstAsync(s => s.ReceiptToken == token);
        var uzsCash = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash && a.Currency == "UZS")).Balance;

        Assert.Equal(expectedChange, sale.ChangeAmount);
        Assert.Equal(-expectedChange, uzsCash);
    }

    [Fact]
    public async Task Usd_debt_sale_and_uzs_repayment_settle_exactly()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12500m);

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand("USD Qarzdor", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 100_000_000m));
            await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)],
                DebtCurrency: "USD"));
        }

        var total = price * 2;
        var usdDebt = Math.Round(total / 12500m, 2);
        Assert.Equal(usdDebt, await BalanceAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt && a.Currency == "USD"));

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new RepayCustomerDebtCommand(customerId, Math.Round(usdDebt * 12500m, 2), false,
                DebtCurrency: "USD", PayCurrency: "UZS"));
        }

        Assert.Equal(0m, await BalanceAsync(a => a.CustomerId == customerId && a.Type == AccountType.Debt && a.Currency == "USD"));
    }

    [Fact]
    public async Task Usd_priced_product_converts_to_base_at_sale()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12000m);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SetProductPriceCommand(variantId, null, 2m, "USD"));
        }

        string token;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            token = (await sender.Send(new CreateSaleCommand(warehouse1, null, 24000m, 0, 0, [new CreateSaleItemDto(variantId, 1)]))).ReceiptToken;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(s => s.Items).FirstAsync(s => s.ReceiptToken == token);
        Assert.Equal(24000m, sale.TotalAmount);
        Assert.Equal("USD", sale.Items.First().PriceCurrency);
        Assert.Equal(12000m, sale.Items.First().PriceRate);
    }

    [Fact]
    public async Task Usd_supply_stores_base_cost_and_usd_payable()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        await SetRateAsync("USD", 12000m);

        long supplierId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            supplierId = await sender.Send(new CreateSupplierCommand("USD Ta'minotchi", null));
            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 1m, null)], Currency: "USD"));
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stock = await db.Stocks.OrderByDescending(s => s.Id).FirstAsync(s => s.VariantId == variantId);
        var payable = (await db.Accounts.FirstAsync(a => a.SupplierId == supplierId && a.Type == AccountType.Debt && a.Currency == "USD")).Balance;

        Assert.Equal(12000m, stock.PurchasePrice);
        Assert.Equal(-10m, payable);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new PaySupplierDebtCommand(supplierId, 120_000m, AccountType.Cash, DebtCurrency: "USD", PayCurrency: "UZS"));
        }

        using var check2 = Fixture.CreateScope();
        var db2 = check2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0m, (await db2.Accounts.FirstAsync(a => a.SupplierId == supplierId && a.Type == AccountType.Debt && a.Currency == "USD")).Balance);
    }

    [Fact]
    public async Task Multicurrency_shift_reports_per_currency_expected_and_difference()
    {
        var (branch1, warehouse1, businessId, adminId, variantId, price) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await SetRateAsync("USD", 12600m);

        long shiftId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            shiftId = await sender.Send(new Cartex.Application.Shifts.Commands.OpenShiftCommand(0,
                [new Cartex.Application.Common.Models.CurrencyAmountDto("USD", 5m)]));

            var total = price * 2;
            var usdAmount = 1m;
            var uzsAmount = total - Math.Round(usdAmount * 12600m, 2);
            await sender.Send(new CreateSaleCommand(warehouse1, null, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)],
                Payments:
                [
                    new SalePaymentDto(PaymentMethod.Cash, "USD", usdAmount),
                    new SalePaymentDto(PaymentMethod.Cash, "UZS", uzsAmount)
                ]));
        }

        using var scope2 = Fixture.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var report = await sender2.Send(new Cartex.Application.Shifts.Commands.CloseShiftCommand(shiftId,
            price * 2 - 12600m,
            [new Cartex.Application.Common.Models.CurrencyAmountDto("USD", 6m)]));

        var usd = report.Currencies.Single(c => c.Currency == "USD");
        Assert.Equal(5m, usd.OpeningFloat);
        Assert.Equal(1m, usd.CashSales);
        Assert.Equal(6m, usd.ExpectedCash);
        Assert.Equal(0m, usd.Difference);
        Assert.Equal(0m, report.Difference);
    }
}
