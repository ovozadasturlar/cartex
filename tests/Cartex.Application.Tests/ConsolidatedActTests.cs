using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Yig'ma dalolatnoma (DAL-01..DAL-11). Every number here comes from docs/domain-rules.md §10
/// and its acceptance criterion, not from the query implementation.
[Collection("database")]
public class ConsolidatedActTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal UnitPrice = 10_000m;

    /// The document kinds are strings on the wire and the specification does not name them.
    private const string SaleKind = "sale";
    private const string ReturnKind = "return";

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    /// What the act must never touch (DAL-04).
    private sealed record Books(
        decimal Debt, decimal Till, int Transactions, int Sales, int Returns, int Payments, int Refunds);

    /// Two well stocked variants repriced to 10 000 in the base currency, so 100 pieces make the
    /// 1 000 000 of the acceptance criterion exactly.
    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
        var warehouse = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
        var business = await db.Businesses.FirstAsync();
        var admin = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();

        var stocked = db.Stocks.Where(x => x.WarehouseId == warehouse && x.Quantity >= 130).Select(x => x.VariantId);
        var variants = await db.ProductPrices
            .Where(x => x.WarehouseId == null && stocked.Contains(x.VariantId))
            .Select(x => x.VariantId)
            .Distinct()
            .OrderBy(x => x)
            .Take(2)
            .ToListAsync();
        Assert.Equal(2, variants.Count);

        foreach (var price in await db.ProductPrices.Where(x => variants.Contains(x.VariantId)).ToListAsync())
        {
            price.SellingPrice = UnitPrice;
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

    private async Task<long> CreateCustomerAsync(string name)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerCommand(name, "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0m, CreditLimit: 100_000_000m));
    }

    private async Task<long> SellAsync(Setup s, long customerId, decimal paidCash, decimal discount,
        params CreateSaleItemDto[] items)
    {
        using var scope = Fixture.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateSaleCommand(s.Warehouse, customerId, paidCash, 0, 0, [.. items])
            {
                DiscountAmount = discount,
                ApplyAutoDiscount = false,
                UseCustomerAdvance = false,
                DebtDueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
            });
        return result.SaleId;
    }

    private async Task<CustomerReturnCreatedDto> ReturnAsync(long saleId, long variantId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == variantId);
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(await TestReturns.ForItemAsync(db, item.Id, quantity));
    }

    private async Task<ConsolidatedActDto> ActAsync(long customerId, params ConsolidatedActSelection[] documents)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetConsolidatedActQuery(customerId, [.. documents]));
    }

    private static ConsolidatedActSelection SaleDoc(long id) => new(SaleKind, id);

    private static ConsolidatedActSelection ReturnDoc(long id) => new(ReturnKind, id);

    private async Task SetActAllowedAsync(bool allowed)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowConsolidatedAct = allowed });
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts
            .Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .SumAsync(x => x.Balance);
    }

    private async Task<Books> BooksAsync(long customerId, long branchId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new Books(
            await db.Accounts.Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
                .SumAsync(x => x.Balance),
            await db.Accounts.Where(x => x.BranchId == branchId && x.Type == AccountType.Cash)
                .SumAsync(x => x.Balance),
            await db.Transactions.CountAsync(),
            await db.Sales.CountAsync(),
            await db.CustomerReturnDocuments.CountAsync(),
            await db.CustomerPaymentDocuments.CountAsync(),
            await db.CustomerRefundDocuments.CountAsync());
    }

    [Fact]
    public async Task DAL_02_Act_over_a_sale_and_its_partial_return_shows_the_goods_actually_kept()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("Dalolatnoma qabul mezoni");

        // 100 dona x 10 000 = 1 000 000, mijoz 600 000 to'ladi -> qarz 400 000
        var saleId = await SellAsync(s, customerId, 600_000m, 0m, new CreateSaleItemDto(s.VariantA, 100));
        Assert.Equal(400_000m, await DebtAsync(customerId));

        // 30 dona x 10 000 = 300 000 qaytdi -> 400 000 - 300 000 = 100 000
        var returned = await ReturnAsync(saleId, s.VariantA, 30);
        Assert.Equal(300_000m, returned.RefundAmount);
        Assert.Equal(100_000m, await DebtAsync(customerId));

        var act = await ActAsync(customerId, SaleDoc(saleId), ReturnDoc(returned.Id));

        // (1) tanlangan hujjatlar ro'yxati: ikkalasi ham ko'rinadi
        Assert.Equal(2, act.Documents.Count);
        var saleDocument = Assert.Single(act.Documents, x => x.Kind == SaleKind && x.Id == saleId);
        var returnDocument = Assert.Single(act.Documents, x => x.Kind == ReturnKind && x.Id == returned.Id);
        Assert.Equal(1_000_000m, saleDocument.Amount);
        // the sign of a return row is not fixed by the specification, only its size
        Assert.Equal(300_000m, Math.Abs(returnDocument.Amount));

        // (2) sof ishlatilgan mahsulot: 100 - 30 = 70 dona, 70 x 10 000 = 700 000
        var line = Assert.Single(act.Lines);
        Assert.Equal(s.VariantA, line.VariantId);
        Assert.Equal(100m, line.SoldQuantity);
        Assert.Equal(30m, line.ReturnedQuantity);
        Assert.Equal(70m, line.NetQuantity);
        Assert.Equal(700_000m, line.NetAmount);

        // (3) pul yakuni: 700 000 - 600 000 = 100 000
        Assert.Equal(700_000m, act.ConsumedAmount);
        Assert.Equal(600_000m, act.PaidAmount);
        Assert.Equal(100_000m, act.RemainingDebt);
        Assert.Equal(act.ConsumedAmount - act.PaidAmount, act.RemainingDebt);
    }

    [Fact]
    public async Task DAL_05_Fully_returned_product_is_absent_from_the_consumed_lines()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("To'liq qaytarilgan mahsulot");

        // A: 2 x 10 000 = 20 000, B: 1 x 10 000 = 10 000, jami 30 000, hammasi qarzga
        var saleId = await SellAsync(s, customerId, 0m, 0m,
            new CreateSaleItemDto(s.VariantA, 2),
            new CreateSaleItemDto(s.VariantB, 1));
        Assert.Equal(30_000m, await DebtAsync(customerId));

        // B butunlay qaytdi: 1 x 10 000 = 10 000 -> qarz 30 000 - 10 000 = 20 000
        var returned = await ReturnAsync(saleId, s.VariantB, 1);
        Assert.Equal(10_000m, returned.RefundAmount);
        Assert.Equal(20_000m, await DebtAsync(customerId));

        var act = await ActAsync(customerId, SaleDoc(saleId), ReturnDoc(returned.Id));

        // B umuman ko'rinmaydi - nol qatori bilan ham emas
        Assert.DoesNotContain(act.Lines, x => x.VariantId == s.VariantB);
        var line = Assert.Single(act.Lines);
        Assert.Equal(s.VariantA, line.VariantId);
        Assert.Equal(2m, line.NetQuantity);
        Assert.Equal(20_000m, line.NetAmount);

        // 20 000 - 0 = 20 000
        Assert.Equal(20_000m, act.ConsumedAmount);
        Assert.Equal(0m, act.PaidAmount);
        Assert.Equal(20_000m, act.RemainingDebt);
    }

    [Fact]
    public async Task DAL_06_Consumed_amount_uses_the_net_line_value_not_quantity_times_price()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("Chegirmali dalolatnoma");

        // 100 x 10 000 = 1 000 000 brutto, 200 000 chegirma -> sof 800 000
        // 300 000 naqd -> qarz 800 000 - 300 000 = 500 000
        var saleId = await SellAsync(s, customerId, 300_000m, 200_000m, new CreateSaleItemDto(s.VariantA, 100));
        Assert.Equal(500_000m, await DebtAsync(customerId));

        var act = await ActAsync(customerId, SaleDoc(saleId));

        var line = Assert.Single(act.Lines);
        Assert.Equal(100m, line.NetQuantity);
        // sof qiymat 800 000, miqdor x narx bo'lgan 1 000 000 emas
        Assert.Equal(800_000m, line.NetAmount);
        Assert.NotEqual(line.NetQuantity * UnitPrice, line.NetAmount);

        Assert.Equal(800_000m, act.ConsumedAmount);
        Assert.Equal(300_000m, act.PaidAmount);
        // 800 000 - 300 000 = 500 000, ya'ni dalolatnoma qarz bilan mos
        Assert.Equal(500_000m, act.RemainingDebt);
        Assert.Equal(act.ConsumedAmount - act.PaidAmount, act.RemainingDebt);
        Assert.Equal(await DebtAsync(customerId), act.RemainingDebt);
    }

    [Fact]
    public async Task DAL_03_Documents_of_two_customers_cannot_be_mixed_into_one_act()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var first = await CreateCustomerAsync("Dalolatnoma mijoz A");
        var second = await CreateCustomerAsync("Dalolatnoma mijoz B");

        // 1 x 10 000 = 10 000 har bir mijozga
        var firstSale = await SellAsync(s, first, 0m, 0m, new CreateSaleItemDto(s.VariantA, 1));
        var secondSale = await SellAsync(s, second, 0m, 0m, new CreateSaleItemDto(s.VariantA, 1));

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            ActAsync(first, SaleDoc(firstSale), SaleDoc(secondSale)));
        Assert.True(error is BusinessRuleException or ValidationException, error.GetType().Name);

        // the mix is what is refused: the customer's own document alone is accepted
        var act = await ActAsync(first, SaleDoc(firstSale));
        Assert.Equal(first, act.CustomerId);
        Assert.Equal(10_000m, act.ConsumedAmount);
    }

    [Fact]
    public async Task DAL_10_Unselected_return_does_not_reduce_the_net_goods()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("Tanlanmagan qaytarish");

        // 100 x 10 000 = 1 000 000, 600 000 to'landi -> qarz 400 000
        var saleId = await SellAsync(s, customerId, 600_000m, 0m, new CreateSaleItemDto(s.VariantA, 100));
        // 30 x 10 000 = 300 000 qaytdi -> mijozning haqiqiy qarzi 100 000
        var returned = await ReturnAsync(saleId, s.VariantA, 30);
        Assert.Equal(300_000m, returned.RefundAmount);
        Assert.Equal(100_000m, await DebtAsync(customerId));

        // faqat savdo belgilandi, qaytarish tanlanmadi
        var act = await ActAsync(customerId, SaleDoc(saleId));

        var document = Assert.Single(act.Documents);
        Assert.Equal(saleId, document.Id);
        var line = Assert.Single(act.Lines);
        Assert.Equal(100m, line.SoldQuantity);
        Assert.Equal(0m, line.ReturnedQuantity);
        Assert.Equal(100m, line.NetQuantity);
        Assert.Equal(1_000_000m, line.NetAmount);

        // DAL-08: tanlangan hujjat bo'yicha 1 000 000 - 600 000 = 400 000,
        // mijozning umumiy balansi 100 000 bo'lsa ham
        Assert.Equal(1_000_000m, act.ConsumedAmount);
        Assert.Equal(600_000m, act.PaidAmount);
        Assert.Equal(400_000m, act.RemainingDebt);
        Assert.Equal(100_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task DAL_11_Act_without_the_customers_act_permission_is_forbidden()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("Ruxsatsiz dalolatnoma");
        // 1 x 10 000 = 10 000
        var saleId = await SellAsync(s, customerId, 0m, 0m, new CreateSaleItemDto(s.VariantA, 1));

        // a cashier who may see the customer and the sale but may not draw up an act.
        // The first three grants are setup only, so the guard under test is the one that fires.
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.View);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.ViewAll);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.View);

        await Assert.ThrowsAsync<ForbiddenException>(() => ActAsync(customerId, SaleDoc(saleId)));

        // that one permission is the whole gate
        Fixture.CurrentUser.Granted.Add(AppPermissions.Customers.Act);
        var act = await ActAsync(customerId, SaleDoc(saleId));
        Assert.Equal(customerId, act.CustomerId);
        Assert.Equal(10_000m, act.ConsumedAmount);
    }

    [Fact]
    public async Task DAL_11_Act_is_refused_when_the_policy_switch_is_off()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("O'chirilgan dalolatnoma");
        // 1 x 10 000 = 10 000
        var saleId = await SellAsync(s, customerId, 0m, 0m, new CreateSaleItemDto(s.VariantA, 1));

        await SetActAllowedAsync(false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(customerId, SaleDoc(saleId)));

        await SetActAllowedAsync(true);
        var act = await ActAsync(customerId, SaleDoc(saleId));
        Assert.Equal(customerId, act.CustomerId);
        Assert.Equal(10_000m, act.ConsumedAmount);
    }

    [Fact]
    public async Task DAL_04_Generating_an_act_posts_nothing_and_leaves_the_balances_alone()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);
        var customerId = await CreateCustomerAsync("O'zgartirmaydigan dalolatnoma");

        // 100 x 10 000 = 1 000 000, 600 000 naqd -> qarz 400 000
        var saleId = await SellAsync(s, customerId, 600_000m, 0m, new CreateSaleItemDto(s.VariantA, 100));
        // 30 x 10 000 = 300 000 -> qarz 100 000, kassaga tegmaydi
        var returned = await ReturnAsync(saleId, s.VariantA, 30);
        Assert.Equal(100_000m, await DebtAsync(customerId));

        var before = await BooksAsync(customerId, s.Branch);
        Assert.Equal(100_000m, before.Debt);

        await ActAsync(customerId, SaleDoc(saleId), ReturnDoc(returned.Id));
        await ActAsync(customerId, SaleDoc(saleId), ReturnDoc(returned.Id));

        // qarz, kassa, defter yozuvlari va hujjat sonlari - hammasi o'zgarishsiz
        Assert.Equal(before, await BooksAsync(customerId, s.Branch));
    }
}
