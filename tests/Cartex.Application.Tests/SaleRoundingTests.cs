using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SaleRoundingTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal PriceA = 100_000m;
    private const decimal PriceB = 15_000m;
    private const decimal Gross = PriceA + PriceB;              // 115 000
    private const decimal Manual = 3_450m;                      // 115 000 x 3%
    private const decimal Payable = Gross - Manual;             // 111 550
    private const decimal Rounding = 1_550m;                    // 111 550 -> 110 000
    private const decimal RoundedTotal = Payable - Rounding;    // 110 000

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    /// Two priced variants that both have stock, normalised to the base currency at 100 000 and 15 000
    /// so the allocation maths in the assertions is exact.
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
            price.SellingPrice = price.VariantId == variants[0] ? PriceA : PriceB;
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

    private async Task AsCashierAsync(Setup s, params string[] permissions)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        foreach (var permission in permissions)
            Fixture.CurrentUser.Granted.Add(permission);
    }

    private async Task SetPolicyAsync(decimal maxRounding)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy,
            new SalesPolicySettings { MaxDiscountPercent = 100, MaxRoundingAmount = maxRounding });
    }

    private static CreateSaleCommand Command(Setup s, decimal paid, decimal discount, decimal rounding) =>
        new(s.Warehouse, null, paid, 0, 0,
            [new CreateSaleItemDto(s.VariantA, 1), new CreateSaleItemDto(s.VariantB, 1)],
            discount, ApplyAutoDiscount: false, RoundingAmount: rounding);

    private async Task<long> SellAsync(Setup s, decimal paid, decimal discount, decimal rounding)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(Command(s, paid, discount, rounding))).SaleId;
    }

    private async Task<Exception> RejectedAsync(Setup s, decimal paid, decimal discount, decimal rounding)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await Assert.ThrowsAnyAsync<Exception>(() => sender.Send(Command(s, paid, discount, rounding)));
    }

    [Fact]
    public async Task CHEG_10_Rounding_joins_the_discount_and_the_total_is_what_the_customer_pays()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        // Gross 115 000, 3% off = 3 450 -> payable 111 550; the customer pays 110 000, so 1 550 is rounded away.
        var saleId = await SellAsync(s, RoundedTotal, Manual, Rounding);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(Manual + Rounding, sale.DiscountAmount);   // 3 450 + 1 550 = 5 000
        Assert.Equal(Rounding, sale.RoundingAmount);            // 1 550
        Assert.Equal(RoundedTotal, sale.TotalAmount);           // 115 000 - 5 000 = 110 000
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));

        foreach (var item in sale.Items)
        {
            Assert.True(item.DiscountAmount >= 0, $"line discount {item.DiscountAmount} is negative");
            Assert.True(item.DiscountAmount <= item.Quantity * item.UnitPrice,
                $"line discount {item.DiscountAmount} exceeds line total {item.Quantity * item.UnitPrice}");
        }
    }

    [Fact]
    public async Task QAYT_01_Rounding_allocated_to_a_line_comes_back_with_that_line()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        var saleId = await SellAsync(s, RoundedTotal, Manual, Rounding);

        // Both components spread over the net values 100 000 : 15 000.
        // Manual  3 450 -> A 3 000.00, B   450.00
        // Rounding 1 550 -> A 1 347.83, B   202.17   (1 550 x 100/115 and x 15/115)
        // Line A discount 4 347.83 -> net 95 652.17; line B discount 652.17 -> net 14 347.83.
        const decimal netA = 95_652.17m;
        const decimal netB = 14_347.83m;

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);
            Assert.Equal(4_347.83m, sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount));
            Assert.Equal(652.17m, sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
        }

        decimal refundB;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var lineB = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantB);
            refundB = (await sender.Send(await TestReturns.ForItemAsync(db, lineB.Id, 1))).RefundAmount;
        }

        // 14 550 would mean the rounding never reached this line.
        Assert.Equal(netB, refundB);

        decimal refundA;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var lineA = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId && x.VariantId == s.VariantA);
            refundA = (await sender.Send(await TestReturns.ForItemAsync(db, lineA.Id, 1))).RefundAmount;
        }

        Assert.Equal(netA, refundA);
        Assert.Equal(RoundedTotal, refundA + refundB);
    }

    [Fact]
    public async Task CHEG_12_Negative_rounding_is_rejected()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        // Everything else is a valid, fully paid sale: only the -1 000 may be the reason for the refusal.
        var error = await RejectedAsync(s, Payable, Manual, -1_000m);

        Assert.True(error is ValidationException or BusinessRuleException,
            $"expected a rejection, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task CHEG_12_Rounding_beyond_the_payable_is_rejected()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        // Payable after the 3% is 111 550; one tiyin more than that cannot be rounded away.
        var error = await RejectedAsync(s, Payable, Manual, Payable + 0.01m);

        Assert.True(error is ValidationException or BusinessRuleException,
            $"expected a rejection, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task CHEG_12_Rounding_above_the_policy_limit_requires_the_override_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync(maxRounding: 1_000m);
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        // 1 550 rounded away against a 1 000 policy ceiling, without sales.discountOverride.
        var error = await RejectedAsync(s, Gross - Rounding, 0m, Rounding);

        Assert.IsType<ForbiddenException>(error);
    }

    [Fact]
    public async Task CHEG_12_Rounding_above_the_policy_limit_is_allowed_with_the_override_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync(maxRounding: 1_000m);
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout,
            AppPermissions.Sales.Discount, AppPermissions.Sales.DiscountOverride);

        // Rounding is the only discount here: 115 000 - 1 550 = 113 450.
        var saleId = await SellAsync(s, Gross - Rounding, 0m, Rounding);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(Rounding, sale.RoundingAmount);
        Assert.Equal(Rounding, sale.DiscountAmount);
        Assert.Equal(Gross - Rounding, sale.TotalAmount);
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
    }

    [Fact]
    public async Task CHEG_13_Manual_discount_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync(maxRounding: 100_000m);
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        var error = await RejectedAsync(s, Payable, Manual, 0m);

        Assert.IsType<ForbiddenException>(error);
    }

    [Fact]
    public async Task CHEG_13_Rounding_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync(maxRounding: 100_000m);
        await AsCashierAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        // The 1 550 is well inside the policy ceiling, so only the missing sales.discount can refuse it.
        var error = await RejectedAsync(s, Gross - Rounding, 0m, Rounding);

        Assert.IsType<ForbiddenException>(error);
    }

    [Fact]
    public async Task CHEG_11_Sale_without_rounding_reports_zero()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        var saleId = await SellAsync(s, Payable, Manual, 0m);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(0m, sale.RoundingAmount);
        Assert.Equal(Manual, sale.DiscountAmount);
        Assert.Equal(Payable, sale.TotalAmount);        // 115 000 - 3 450
        Assert.Equal(sale.DiscountAmount, sale.Items.Sum(i => i.DiscountAmount));
    }

    [Fact]
    public async Task CHEG_11_Rounding_is_reported_without_changing_the_total_formula()
    {
        var s = await SetupAsync();
        await AsAdminAsync(s);

        var saleId = await SellAsync(s, RoundedTotal, Manual, Rounding);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        var gross = sale.Items.Sum(i => i.Quantity * i.UnitPrice);
        Assert.Equal(Gross, gross);

        // CHEG-08 stays "total = gross - discount": the rounding is already inside the discount,
        // so subtracting it a second time (108 450) would be wrong.
        Assert.Equal(gross - sale.DiscountAmount, sale.TotalAmount);
        Assert.True(sale.RoundingAmount <= sale.DiscountAmount,
            $"rounding {sale.RoundingAmount} is larger than the discount {sale.DiscountAmount} it belongs to");
        Assert.Equal(Rounding, sale.RoundingAmount);
    }
}
