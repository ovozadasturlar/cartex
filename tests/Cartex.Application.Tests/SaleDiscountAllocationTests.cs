using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SaleDiscountAllocationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 100_000m;

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    /// Two priced variants that both have stock, normalised to the same base-currency price so the
    /// allocation maths in the assertions is exact.
    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = await db.Businesses.FirstAsync();
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
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
            price.SellingPrice = Price;
            price.Currency = business.Currency;
        }
        await db.SaveChangesAsync();

        return new Setup(branch, warehouse, business.Id, admin, variants[0], variants[1]);
    }

    private async Task<long> SellAsync(Setup s, decimal paid, decimal discount, params CreateSaleItemDto[] items)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(
            s.Warehouse, null, paid, 0, 0, [.. items], discount, ApplyAutoDiscount: false));
        return result.SaleId;
    }

    [Fact]
    public async Task QAYT_01_Targeted_price_cut_does_not_reduce_the_refund_of_another_line()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        const decimal cut = 60_000m;
        var saleId = await SellAsync(s, cut + Price, 0,
            new CreateSaleItemDto(s.VariantA, 1, cut),
            new CreateSaleItemDto(s.VariantB, 1));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.SingleAsync(x => x.Id == saleId);
            Assert.Equal(Price - cut, sale.DiscountAmount);
            Assert.Equal(Price + cut, sale.TotalAmount);
        }

        decimal refund;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var lineB = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantB);
            refund = (await sender.Send(await TestReturns.ForItemAsync(db, lineB.Id, 1))).RefundAmount;
        }

        // B was sold at the full catalog price; A's cut must not be smeared onto it.
        Assert.Equal(Price, refund);
    }

    [Fact]
    public async Task CHEG_04_Price_cut_lands_only_on_the_line_it_was_entered_for()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        const decimal cut = 60_000m;
        var saleId = await SellAsync(s, cut + Price, 0,
            new CreateSaleItemDto(s.VariantA, 1, cut),
            new CreateSaleItemDto(s.VariantB, 1));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(Price - cut, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
        Assert.Equal(0m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
    }

    [Fact]
    public async Task CHEG_02_Manual_discount_spreads_across_lines_and_sums_exactly()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        // 100 000 + 200 000 gross, 10 000 off: the shares do not divide evenly.
        const decimal discount = 10_000m;
        var saleId = await SellAsync(s, 290_000m, discount,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 2));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(discount, sale.DiscountAmount);
        Assert.Equal(discount, sale.Items.Sum(i => i.DiscountAmount));
        Assert.Equal(3_333.33m, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
        Assert.Equal(6_666.67m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
    }

    [Fact]
    public async Task CHEG_05_Order_discount_follows_the_price_the_customer_actually_pays()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        // 100 000 + 100 000 with the second cut to 80 000, then 2% off the 180 000 payable.
        var saleId = await SellAsync(s, 176_400m, 3_600m,
            new CreateSaleItemDto(s.VariantA, 1),
            new CreateSaleItemDto(s.VariantB, 1, 80_000m));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

            // 2% of 100 000, not an even split of the 3 600.
            Assert.Equal(2_000m, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
            Assert.Equal(21_600m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
            Assert.Equal(23_600m, sale.DiscountAmount);
            Assert.Equal(176_400m, sale.TotalAmount);
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var lineB = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantB);
            Assert.Equal(78_400m, (await sender.Send(await TestReturns.ForItemAsync(db, lineB.Id, 1))).RefundAmount);
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var lineA = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantA);
            Assert.Equal(98_000m, (await sender.Send(await TestReturns.ForItemAsync(db, lineA.Id, 1))).RefundAmount);
        }
    }

    [Fact]
    public async Task QAYT_02_Repeated_partial_returns_sum_exactly_to_the_sale_total()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        // 3 x 100 000 less a 10 000 discount does not divide into three equal refunds.
        var saleId = await SellAsync(s, 290_000m, 10_000m, new CreateSaleItemDto(s.VariantA, 3));

        var refunded = 0m;
        for (var i = 0; i < 3; i++)
        {
            using var scope = Fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var line = await db.SaleItems.AsNoTracking()
                .FirstAsync(x => x.SaleId == saleId && x.Quantity > x.ReturnedQuantity);
            refunded += (await sender.Send(await TestReturns.ForItemAsync(db, line.Id, 1))).RefundAmount;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.SingleAsync(x => x.Id == saleId);
            Assert.Equal(sale.TotalAmount, refunded);
            Assert.Equal(290_000m, refunded);
        }
    }

    [Fact]
    public async Task QAYT_03_Fully_discounted_line_can_still_be_returned()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        var saleId = await SellAsync(s, Price, 0,
            new CreateSaleItemDto(s.VariantA, 1, 0m),
            new CreateSaleItemDto(s.VariantB, 1));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var lineA = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantA);

        var result = await sender.Send(await TestReturns.ForItemAsync(db, lineA.Id, 1));

        Assert.Equal(0m, result.RefundAmount);
        var returned = await db.SaleItems.AsNoTracking().FirstAsync(x => x.Id == lineA.Id);
        Assert.Equal(1m, returned.ReturnedQuantity);
    }

    [Fact]
    public async Task CHEG_03_Discount_never_drives_a_line_below_zero()
    {
        var s = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);

        // A is cut by 90 000 already; a further 110 000 off cannot take more than A has left.
        var saleId = await SellAsync(s, 0m, 110_000m,
            new CreateSaleItemDto(s.VariantA, 1, 10_000m),
            new CreateSaleItemDto(s.VariantB, 1));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(0m, sale.TotalAmount);
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
        foreach (var item in sale.Items)
        {
            Assert.True(item.DiscountAmount >= 0, $"line discount {item.DiscountAmount} is negative");
            Assert.True(item.DiscountAmount <= item.Quantity * item.UnitPrice,
                $"line discount {item.DiscountAmount} exceeds line total {item.Quantity * item.UnitPrice}");
        }
    }
}
