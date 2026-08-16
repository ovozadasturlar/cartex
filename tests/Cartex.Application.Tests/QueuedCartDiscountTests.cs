using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md §7 (NAVBAT-01, NAVBAT-02, NAVBAT-03, NAVBAT-05) alone, per DR-01.
[Collection("database")]
public sealed class QueuedCartDiscountTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal PriceA = 100_000m;
    private const decimal PriceB = 15_000m;
    private const decimal Gross = PriceA + PriceB;              // 115 000
    private const decimal Manual = 3_450m;                      // 115 000 x 3%
    private const decimal Rounding = 1_550m;                    // 111 550 -> 110 000
    private const decimal SaleDiscount = Manual + Rounding;     // CHEG-10: the rounding joins the discount = 5 000
    private const decimal RoundedTotal = Gross - SaleDiscount;  // CHEG-08: 115 000 - 5 000 = 110 000

    // CHEG-05: the 5 000 spreads over the net line values 100 000 : 15 000, not equally.
    // A: 5 000 x 100/115 = 4 347.826... -> 4 347.83
    // B: 5 000 x  15/115 =   652.173... ->   652.17     (PUL-03: the parts add up to exactly 5 000)
    private const decimal LineDiscountA = 4_347.83m;
    private const decimal LineDiscountB = 652.17m;

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    private sealed record SaleFacts(decimal Discount, decimal Rounding, decimal Total, decimal LineSum, decimal LineA, decimal LineB);

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

    /// Puts the policy ceilings out of the way so that only `sales.discount` (CHEG-13) can refuse anything here.
    private async Task SetPolicyAsync()
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy,
            new SalesPolicySettings { MaxDiscountPercent = 100, MaxRoundingAmount = 100_000m });
    }

    private async Task StartAsync(Setup s, params string[] permissions)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        foreach (var permission in permissions)
            Fixture.CurrentUser.Granted.Add(permission);
    }

    private static SubmitCartCommand Submit(Setup s, decimal discount, decimal rounding) =>
        new(s.Warehouse, null,
            [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)],
            DiscountAmount: discount, RoundingAmount: rounding);

    private async Task<string> QueueAsync(Setup s, decimal discount, decimal rounding)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(Submit(s, discount, rounding));
    }

    private async Task<long> CheckoutAsync(CheckoutCartCommand command)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private async Task StatusAsync(string code, CartStatus status, string? reason = null)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCartStatusCommand(code, status, reason));
    }

    private async Task<(decimal Discount, decimal Rounding)> CartAsync(string code)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = await db.Carts.AsNoTracking().SingleAsync(x => x.AggregateCode == code);
        return (cart.DiscountAmount, cart.RoundingAmount);
    }

    private async Task<SaleFacts> SaleAsync(Setup s, long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);
        return new SaleFacts(
            sale.DiscountAmount,
            sale.RoundingAmount,
            sale.TotalAmount,
            sale.Items.Sum(i => i.DiscountAmount),
            sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount),
            sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
    }

    /// NAVBAT-01 worked criterion: the seller queues 3 450 off plus 1 550 rounded away on a 115 000 basket,
    /// the cashier changes nothing, and the sale must read 5 000 / 1 550 / 110 000.
    [Fact]
    public async Task NAVBAT_01_Queued_discount_and_rounding_reach_the_sale_untouched()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        // First the queue itself must hold the values, otherwise the loss happened before checkout.
        var cart = await CartAsync(code);
        Assert.Equal(Manual, cart.Discount);            // 3 450
        Assert.Equal(Rounding, cart.Rounding);          // 1 550

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, RoundedTotal, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(SaleDiscount, sale.Discount);      // 3 450 + 1 550 = 5 000
        Assert.Equal(Rounding, sale.Rounding);          // 1 550 reported on its own (CHEG-11)
        Assert.Equal(RoundedTotal, sale.Total);         // 115 000 - 5 000 = 110 000
        Assert.Equal(SaleDiscount, sale.LineSum);       // CHEG-02: the lines add up to the header exactly
        Assert.Equal(LineDiscountA, sale.LineA);        // 5 000 x 100/115 = 4 347.83
        Assert.Equal(LineDiscountB, sale.LineB);        // 5 000 x  15/115 =   652.17
    }

    /// NAVBAT-05: the cashier who finishes the cart has no `sales.discount`; the seller who entered
    /// the values had it, so they are already authorised and must not be asked for again.
    [Fact]
    public async Task NAVBAT_05_Queued_discount_survives_a_cashier_without_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.Discount);

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, RoundedTotal, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(SaleDiscount, sale.Discount);      // 5 000
        Assert.Equal(Rounding, sale.Rounding);          // 1 550
        Assert.Equal(RoundedTotal, sale.Total);         // 110 000
        Assert.Equal(SaleDiscount, sale.LineSum);
    }

    /// NAVBAT-05, second sentence: a value the cashier changes is a value the cashier is entering.
    [Fact]
    public async Task NAVBAT_05_Changing_the_queued_discount_at_checkout_requires_the_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.Discount);

        // 10 000 is not the queued 3 450. The payment matches what the sale would come to
        // (115 000 - (10 000 + 1 550) = 103 450), so only the missing permission can refuse it.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CheckoutAsync(new CheckoutCartCommand(code, 103_450m, 0, 0, DiscountAmount: 10_000m)));
    }

    /// NAVBAT-05: the rounding is guarded exactly like the discount it belongs to.
    [Fact]
    public async Task NAVBAT_05_Changing_the_queued_rounding_at_checkout_requires_the_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.Discount);

        // 3 000 is not the queued 1 550; 115 000 - (3 450 + 3 000) = 108 550.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CheckoutAsync(new CheckoutCartCommand(code, 108_550m, 0, 0, RoundingAmount: 3_000m)));
    }

    /// NAVBAT-02: "request if given, cart otherwise" — per field, in the same call.
    [Fact]
    public async Task NAVBAT_02_Request_discount_wins_while_the_cart_rounding_still_rides_along()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        // The cashier has the permission and raises the discount to 6 000; he says nothing about the
        // rounding, so the queued 1 550 stays. 6 000 + 1 550 = 7 550 -> 115 000 - 7 550 = 107 450.
        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, 107_450m, 0, 0, DiscountAmount: 6_000m));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(7_550m, sale.Discount);
        Assert.Equal(Rounding, sale.Rounding);          // 1 550 came from the cart, not from the request
        Assert.Equal(107_450m, sale.Total);
        Assert.Equal(7_550m, sale.LineSum);             // CHEG-02
    }

    /// NAVBAT-03 (with NAVBAT-01): requeue copies every field, discount and rounding among them.
    [Fact]
    public async Task NAVBAT_03_Requeued_cart_keeps_the_discount_and_the_rounding()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);

        var code = await QueueAsync(s, Manual, Rounding);

        await StatusAsync(code, CartStatus.Confirmed);
        await StatusAsync(code, CartStatus.Cancelled, "Mijoz kutib turdi");

        string requeued;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            requeued = (await sender.Send(new RequeueCartCommand(code, "Qayta navbatga"))).AggregateCode;
        }

        var clone = await CartAsync(requeued);
        Assert.Equal(Manual, clone.Discount);           // 3 450
        Assert.Equal(Rounding, clone.Rounding);         // 1 550
    }

    /// NAVBAT-01 through the edit path: the phone reopens a queued cart and saves it again.
    [Fact]
    public async Task NAVBAT_01_Editing_a_queued_cart_keeps_the_discount_through_checkout()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual, Rounding);

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCartCommand(
                code, null,
                [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)],
                DiscountAmount: Manual, RoundingAmount: Rounding));
        }

        var edited = await CartAsync(code);
        Assert.Equal(Manual, edited.Discount);
        Assert.Equal(Rounding, edited.Rounding);

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, RoundedTotal, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(SaleDiscount, sale.Discount);      // 5 000
        Assert.Equal(Rounding, sale.Rounding);          // 1 550
        Assert.Equal(RoundedTotal, sale.Total);         // 110 000
        Assert.Equal(SaleDiscount, sale.LineSum);
    }

    /// NAVBAT-01 from the other side: a cart nobody discounted must not pick one up on the way through.
    [Fact]
    public async Task NAVBAT_01_Cart_without_a_discount_checks_out_with_zero()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        var code = await QueueAsync(s, 0m, 0m);

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Gross, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(0m, sale.Discount);
        Assert.Equal(0m, sale.Rounding);
        Assert.Equal(Gross, sale.Total);                // 115 000, nothing taken off
        Assert.Equal(0m, sale.LineSum);
    }

    /// CHEG-13: the pre-authorisation in NAVBAT-05 is only legitimate because the entry point is guarded.
    [Fact]
    public async Task CHEG_13_Submitting_a_discount_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(Submit(s, Manual, Rounding)));
    }

    /// CHEG-13 on the edit path: the guard has to sit on every door into the queue, not only the first one.
    [Fact]
    public async Task CHEG_13_Editing_a_discount_into_a_queued_cart_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create);

        var code = await QueueAsync(s, 0m, 0m);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(new UpdateCartCommand(
            code, null,
            [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)],
            DiscountAmount: Manual, RoundingAmount: Rounding)));
    }
}
