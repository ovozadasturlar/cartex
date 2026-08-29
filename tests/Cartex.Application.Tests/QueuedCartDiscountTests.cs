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
    private const decimal Manual = 5_000m;                      // sotuvchi kelishgan chegirma
    private const decimal Payable = Gross - Manual;             // CHEG-08: 115 000 - 5 000 = 110 000

    // CHEG-05: the 5 000 spreads over the net line values 100 000 : 15 000, not equally.
    // A: 5 000 x 100/115 = 4 347.826... -> 4 347.83
    // B: 5 000 x  15/115 =   652.173... ->   652.17     (PUL-03: the parts add up to exactly 5 000)
    private const decimal LineDiscountA = 4_347.83m;
    private const decimal LineDiscountB = 652.17m;

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long VariantA, long VariantB);

    private sealed record SaleFacts(decimal Discount, decimal Total, decimal LineSum, decimal LineA, decimal LineB);

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
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { MaxDiscountPercent = 100 });
    }

    private async Task StartAsync(Setup s, params string[] permissions)
    {
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
        Fixture.CurrentUser.AsCashier(s.Admin, s.Business, s.Branch);
        foreach (var permission in permissions)
            Fixture.CurrentUser.Granted.Add(permission);
    }

    private static SubmitCartCommand Submit(Setup s, decimal discount) =>
        new(s.Warehouse, null,
            [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)])
        {
            DiscountAmount = discount
        };

    private async Task<string> QueueAsync(Setup s, decimal discount)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(Submit(s, discount));
    }

    private async Task<long> CheckoutAsync(CheckoutCartCommand command)
    {
        using var scope = Fixture.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ISender>().Send(command)).SaleId;
    }

    private async Task StatusAsync(string code, CartStatus status, string? reason = null)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCartStatusCommand(code, status, reason));
    }

    private async Task<decimal> CartDiscountAsync(string code)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = await db.Carts.AsNoTracking().SingleAsync(x => x.AggregateCode == code);
        return cart.DiscountAmount;
    }

    private async Task<SaleFacts> SaleAsync(Setup s, long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);
        return new SaleFacts(
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.Items.Sum(i => i.DiscountAmount),
            sale.Items.Where(i => i.VariantId == s.VariantA).Sum(i => i.DiscountAmount),
            sale.Items.Where(i => i.VariantId == s.VariantB).Sum(i => i.DiscountAmount));
    }

    /// NAVBAT-01 worked criterion: the seller queues 5 000 off a 115 000 basket, the cashier
    /// changes nothing, and the sale must read 5 000 / 110 000.
    [Fact]
    public async Task NAVBAT_01_Queued_discount_reaches_the_sale_untouched()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual);

        // First the queue itself must hold the value, otherwise the loss happened before checkout.
        Assert.Equal(Manual, await CartDiscountAsync(code));

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Payable, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(Manual, sale.Discount);            // 5 000
        Assert.Equal(Payable, sale.Total);              // 115 000 - 5 000 = 110 000
        Assert.Equal(Manual, sale.LineSum);             // CHEG-02: the lines add up to the header exactly
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

        var code = await QueueAsync(s, Manual);

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.Discount);

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Payable, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(Manual, sale.Discount);            // 5 000
        Assert.Equal(Payable, sale.Total);              // 110 000
        Assert.Equal(Manual, sale.LineSum);
    }

    /// NAVBAT-05, second sentence: a value the cashier changes is a value the cashier is entering.
    [Fact]
    public async Task NAVBAT_05_Changing_the_queued_discount_at_checkout_requires_the_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual);

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.Discount);

        // 10 000 is not the queued 5 000. The payment matches what the sale would come to
        // (115 000 - 10 000 = 105 000), so only the missing permission can refuse it.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CheckoutAsync(new CheckoutCartCommand(code, 105_000m, 0, 0)
            {
                DiscountAmount = 10_000m
            }));
    }

    /// NAVBAT-02: "request if given, cart otherwise".
    [Fact]
    public async Task NAVBAT_02_Request_discount_wins_over_the_queued_one()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual);

        // The cashier has the permission and raises the discount to 6 000 -> 115 000 - 6 000 = 109 000.
        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, 109_000m, 0, 0)
        {
            DiscountAmount = 6_000m
        });
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(6_000m, sale.Discount);
        Assert.Equal(109_000m, sale.Total);
        Assert.Equal(6_000m, sale.LineSum);             // CHEG-02
    }

    /// NAVBAT-03 (with NAVBAT-01): requeue copies every field, the discount among them.
    [Fact]
    public async Task NAVBAT_03_Requeued_cart_keeps_the_discount()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);

        var code = await QueueAsync(s, Manual);

        await StatusAsync(code, CartStatus.Confirmed);
        await StatusAsync(code, CartStatus.Cancelled, "Mijoz kutib turdi");

        string requeued;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            requeued = (await sender.Send(new RequeueCartCommand(code, "Qayta navbatga"))).AggregateCode;
        }

        Assert.Equal(Manual, await CartDiscountAsync(requeued));   // 5 000
    }

    /// NAVBAT-02 says an omitted field keeps its stored value. An edit that only changes the
    /// items must therefore not silently wipe the discount the seller agreed with the customer.
    [Fact]
    public async Task NAVBAT_02_Editing_a_cart_without_resending_the_discount_keeps_it()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual);

        using (var scope = Fixture.CreateScope())
        {
            // Only the items are sent — no discount.
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCartCommand(
                code, null,
                [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)]));
        }

        Assert.Equal(Manual, await CartDiscountAsync(code));

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Payable, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(Manual, sale.Discount);
        Assert.Equal(Payable, sale.Total);
    }

    [Fact]
    public async Task NAVBAT_01_Editing_a_queued_cart_keeps_the_discount_through_checkout()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout, AppPermissions.Sales.Discount);

        var code = await QueueAsync(s, Manual);

        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new UpdateCartCommand(code, null, [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)])
            {
                DiscountAmount = Manual
            });
        }

        Assert.Equal(Manual, await CartDiscountAsync(code));

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Payable, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(Manual, sale.Discount);            // 5 000
        Assert.Equal(Payable, sale.Total);              // 110 000
        Assert.Equal(Manual, sale.LineSum);
    }

    /// NAVBAT-01 from the other side: a cart nobody discounted must not pick one up on the way through.
    [Fact]
    public async Task NAVBAT_01_Cart_without_a_discount_checks_out_with_zero()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create, AppPermissions.Sales.Checkout);

        var code = await QueueAsync(s, 0m);

        var saleId = await CheckoutAsync(new CheckoutCartCommand(code, Gross, 0, 0));
        var sale = await SaleAsync(s, saleId);

        Assert.Equal(0m, sale.Discount);
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
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(Submit(s, Manual)));
    }

    /// CHEG-13 on the edit path: the guard has to sit on every door into the queue, not only the first one.
    [Fact]
    public async Task CHEG_13_Editing_a_discount_into_a_queued_cart_requires_the_discount_permission()
    {
        var s = await SetupAsync();
        await SetPolicyAsync();
        await StartAsync(s, AppPermissions.Sales.Create);

        var code = await QueueAsync(s, 0m);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(new UpdateCartCommand(code, null, [new SubmitCartItemDto(s.VariantA, 1), new SubmitCartItemDto(s.VariantB, 1)])
        {
            DiscountAmount = Manual
        }));
    }
}
