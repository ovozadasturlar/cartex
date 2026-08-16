using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ScopedAutoDiscountTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 100_000m;

    private sealed record Setup(
        long Branch,
        long Warehouse,
        long Business,
        long Admin,
        long ProductA,
        long VariantA,
        long ProductB,
        long VariantB);

    /// Two priced, stocked variants of two *different* products, both normalised to 100 000 in the
    /// base currency, with the loyalty feature on so automatic rules run.
    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Features.Where(f => f.Code == FeatureCatalog.Loyalty)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsEnabled, true));

        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = await db.Businesses.FirstAsync();
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var priced = db.ProductPrices.Where(p => p.WarehouseId == null).Select(p => p.VariantId);
        var candidates = await db.ProductVariants
            .Where(v => stocked.Contains(v.Id) && priced.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId })
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.NotEmpty(candidates);
        var a = candidates[0];
        var b = candidates.FirstOrDefault(x => x.ProductId != a.ProductId);

        // A product-scoped rule can only single one line out when the lines are different products.
        Assert.NotNull(b);
        Assert.NotEqual(a.ProductId, b!.ProductId);
        Assert.NotEqual(a.Id, b.Id);

        foreach (var price in await db.ProductPrices.Where(p => p.VariantId == a.Id || p.VariantId == b.Id).ToListAsync())
        {
            price.SellingPrice = Price;
            price.Currency = business.Currency;
        }
        await db.SaveChangesAsync();

        return new Setup(branch, warehouse, business.Id, admin, a.ProductId, a.Id, b.ProductId, b.Id);
    }

    private async Task AddRuleAsync(DiscountRule rule)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.DiscountRules.Add(rule);
        await db.SaveChangesAsync();
    }

    /// Pays the full gross so the discount comes back as change and no assertion is ever masked by
    /// a debt or payment failure.
    private async Task<long> SellAsync(Setup s, decimal gross, decimal manualDiscount, params CreateSaleItemDto[] items)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(s.Warehouse, null, gross, 0, 0, [.. items])
        {
            DiscountAmount = manualDiscount
        });
        return result.SaleId;
    }

    [Fact]
    public async Task CHEG_06_Product_rule_discounts_only_its_own_product()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Faqat A mahsulotiga",
            Scope = DiscountScope.Product,
            TargetId = s.ProductA,
            Method = DiscountMethod.Percent,
            Value = 10
        });

        // 100 000 + 100 000 gross; the rule matches line A only.
        var saleId = await SellAsync(s, 200_000m, 0m,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 1));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        var lineA = sale.Items.Where(i => i.VariantId == s.VariantA).ToList();
        var lineB = sale.Items.Where(i => i.VariantId == s.VariantB).ToList();

        // 10% of A's 100 000 = 10 000; nothing else matched, so the sale discount is 10 000.
        Assert.Equal(10_000m, sale.DiscountAmount);

        // All 10 000 sits on A, none of it is smeared onto B (10 000 / 0, not 5 000 / 5 000).
        Assert.Equal(10_000m, lineA.Sum(i => i.DiscountAmount));
        Assert.Equal(0m, lineB.Sum(i => i.DiscountAmount));

        // Nets: 100 000 - 10 000 = 90 000 and 100 000 - 0 = 100 000.
        Assert.Equal(90_000m, lineA.Sum(i => i.Quantity * i.UnitPrice - i.DiscountAmount));
        Assert.Equal(100_000m, lineB.Sum(i => i.Quantity * i.UnitPrice - i.DiscountAmount));

        // CHEG-08: 200 000 - 10 000 = 190 000. CHEG-02: the lines add back up exactly.
        Assert.Equal(190_000m, sale.TotalAmount);
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
    }

    [Fact]
    public async Task CHEG_06_Unmatched_product_is_refunded_in_full()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Faqat A mahsulotiga",
            Scope = DiscountScope.Product,
            TargetId = s.ProductA,
            Method = DiscountMethod.Percent,
            Value = 10
        });

        var saleId = await SellAsync(s, 200_000m, 0m,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 1));

        decimal refundB;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var line = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantB);
            refundB = (await sender.Send(await TestReturns.ForItemAsync(db, line.Id, 1))).RefundAmount;
        }

        // QAYT-01: B's own net was 100 000 because the rule never touched it. Not 95 000, which is
        // what an averaged-out 10 000 across both lines would give.
        Assert.Equal(100_000m, refundB);
    }

    [Fact]
    public async Task CHEG_02_All_scope_rule_spreads_over_every_line_and_sums_exactly()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Hammaga",
            Scope = DiscountScope.All,
            Method = DiscountMethod.FixedAmount,
            Value = 10_000m
        });

        // 1 x 100 000 + 2 x 100 000 = 300 000 gross; the rule matches both lines.
        var saleId = await SellAsync(s, 300_000m, 0m,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 2));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(10_000m, sale.DiscountAmount);

        // 10 000 x 100 000/300 000 = 3 333.33 and 10 000 x 200 000/300 000 = 6 666.67.
        Assert.Equal(3_333.33m, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
        Assert.Equal(6_666.67m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));

        // 3 333.33 + 6 666.67 = 10 000 exactly; 300 000 - 10 000 = 290 000.
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
        Assert.Equal(290_000m, sale.TotalAmount);
    }

    [Fact]
    public async Task CHEG_02_Manual_discount_and_product_rule_stay_inside_their_own_lines()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        await AddRuleAsync(new DiscountRule
        {
            Name = "Faqat A mahsulotiga",
            Scope = DiscountScope.Product,
            TargetId = s.ProductA,
            Method = DiscountMethod.Percent,
            Value = 10
        });

        // 100 000 + 100 000 gross, 10 000 entered by hand on top of the product rule.
        var saleId = await SellAsync(s, 200_000m, 10_000m,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 1));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        // CHEG-05: the manual 10 000 splits by net-of-price-cut value, 100 000 : 100 000 = 5 000 / 5 000.
        // CHEG-06: the rule's 10% of A's 100 000 = 10 000 goes to A alone.
        // A = 5 000 + 10 000 = 15 000, B = 5 000, sale = 20 000, total = 200 000 - 20 000 = 180 000.
        Assert.Equal(5_000m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
        Assert.Equal(15_000m, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
        Assert.Equal(20_000m, sale.DiscountAmount);
        Assert.Equal(180_000m, sale.TotalAmount);

        // CHEG-02 and CHEG-03 hold whatever the split turns out to be.
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
        foreach (var item in sale.Items)
        {
            Assert.True(item.DiscountAmount >= 0, $"line discount {item.DiscountAmount} is negative");
            Assert.True(item.DiscountAmount <= item.Quantity * item.UnitPrice,
                $"line discount {item.DiscountAmount} exceeds line total {item.Quantity * item.UnitPrice}");
        }
    }
}
