using Cartex.Application.Common.Messaging;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Prepacks.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Rates.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.Ordering;
using Cartex.Shared.Models.Prepacks;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SaleExpectedPriceTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Seen = 10_000m;
    private const decimal Moved = 12_000m;
    private const string ProductName = "Narx tekshiruvi mahsuloti";

    private sealed record Ctx(long Branch, long Warehouse, long Business, long AdminId, long VariantId);

    private async Task<Ctx> SetupAsync(decimal catalogPrice)
    {
        long branch, warehouse, business, admin, unit;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
            warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
            business = (await db.Businesses.FirstAsync()).Id;
            admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
            unit = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
        }

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        return new Ctx(branch, warehouse, business, admin, await AddVariantAsync(warehouse, unit, ProductName, catalogPrice));
    }

    private async Task<long> AddVariantAsync(long warehouse, long unit, string name, decimal catalogPrice)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var productId = await sender.Send(new CreateProductCommand(name, null, unit, 0, null));
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, 50, 4_000m, null, SellingPrice: catalogPrice)]));
        return variantId;
    }

    private async Task SetCatalogPriceAsync(long variantId, decimal price)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.ProductPrices.Where(p => p.VariantId == variantId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.SellingPrice, price));
    }

    private static CreateSaleCommand Sell(Ctx ctx, decimal paid, decimal? expectedUnitPrice, decimal quantity = 1) =>
        new(ctx.Warehouse, null, paid, 0, 0,
            [new CreateSaleItemDto(ctx.VariantId, quantity, ExpectedUnitPrice: expectedUnitPrice)])
        {
            ApplyAutoDiscount = false
        };

    private async Task<(int Sales, decimal Stock)> LedgerAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Sales.CountAsync(),
            await db.Stocks.Where(s => s.VariantId == ctx.VariantId && s.WarehouseId == ctx.Warehouse).SumAsync(s => s.Quantity));
    }

    // NARX-11
    private static IReadOnlyList<PriceChangeDto> Changes(DomainException error) => error.Details is IEnumerable<PriceChangeDto> rows
        ? [.. rows]
        : throw new XunitException($"price_changed must report a list of changed lines, details were '{error.Details?.GetType().Name ?? "null"}'");

    private async Task<DomainException> RefusedAsync(Func<ISender, Task> act)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await Assert.ThrowsAnyAsync<DomainException>(() => act(sender));
    }

    private async Task SetRateAsync(string code, decimal rate)
    {
        using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SetExchangeRateCommand(code, rate));
    }

    private async Task<decimal> CatalogNumberAsync(long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.ProductPrices.AsNoTracking().SingleAsync(p => p.VariantId == variantId)).SellingPrice;
    }

    private async Task<long> UnitAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
    }

    private async Task<string> QueueAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new SubmitCartCommand(ctx.Warehouse, null, [new SubmitCartItemDto(ctx.VariantId, 1)]));
    }

    private async Task<long> SellAsync(CreateSaleCommand command)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(command)).SaleId;
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_A_catalog_price_rise_refuses_the_sale_and_reports_the_new_price()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(Sell(ctx, paid: Seen, expectedUnitPrice: Seen)));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(ctx.VariantId, change.VariantId);
        Assert.Equal(Seen, change.Expected);
        Assert.Equal(Moved, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_A_catalog_price_drop_refuses_the_sale_and_reports_the_new_price()
    {
        var ctx = await SetupAsync(Moved);
        await SetCatalogPriceAsync(ctx.VariantId, Seen);
        var before = await LedgerAsync(ctx);

        await SetRateAsync("USD", 13_000m);

        var error = await RefusedAsync(x => x.Send(Sell(ctx, paid: Moved, expectedUnitPrice: Moved)));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(ctx.VariantId, change.VariantId);
        Assert.Equal(Moved, change.Expected);
        Assert.Equal(Seen, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_An_unchanged_catalog_price_lets_the_sale_through()
    {
        var ctx = await SetupAsync(Seen);
        var before = await LedgerAsync(ctx);

        var saleId = await SellAsync(Sell(ctx, paid: Seen, expectedUnitPrice: Seen));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        var item = await db.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == saleId);
        Assert.Equal(Seen, sale.TotalAmount);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(Seen, item.UnitPrice);
        Assert.Equal(before.Stock - 1, (await LedgerAsync(ctx)).Stock);
    }

    // NARX-10
    [Fact]
    public async Task NARX_10_Without_a_seen_price_the_check_is_skipped()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        var saleId = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: null));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        var item = await db.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == saleId);
        Assert.Equal(Moved, sale.TotalAmount);
        Assert.Equal(Moved, item.UnitPrice);
    }

    // NARX-10
    [Fact]
    public async Task NARX_10_A_prepack_line_is_not_checked_against_the_catalog_price()
    {
        var ctx = await SetupAsync(Seen);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var feature = await db.Features.FirstOrDefaultAsync(f => f.Code == FeatureCatalog.Prepack);
            if (feature is null)
                db.Features.Add(new Feature { Code = FeatureCatalog.Prepack, Name = "Qadoq", IsEnabled = true, OwnerEnabled = true });
            else
                (feature.IsEnabled, feature.OwnerEnabled) = (true, true);
            await db.SaveChangesAsync();
        }

        PrepackLabelDto label;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            label = (await sender.Send(new CreatePrepacksCommand(ctx.Warehouse, ctx.VariantId, 1))).Single();
        }
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        long prepackId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            prepackId = (await db.Prepacks.SingleAsync(p => p.LabelCode == label.LabelCode)).Id;
        }

        var saleId = await SellAsync(new CreateSaleCommand(ctx.Warehouse, null, label.Price, 0, 0,
            [new CreateSaleItemDto(ctx.VariantId, 1, PrepackId: prepackId, ExpectedUnitPrice: Seen)])
        { ApplyAutoDiscount = false });

        using var check = Fixture.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await context.Sales.AnyAsync(s => s.Id == saleId));
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_The_seen_price_is_not_an_override_and_needs_no_price_permission()
    {
        var ctx = await SetupAsync(Seen);
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Create);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Checkout);
        Assert.False(Fixture.CurrentUser.HasPermission(AppPermissions.Sales.PriceOverride));

        var saleId = await SellAsync(Sell(ctx, paid: Seen, expectedUnitPrice: Seen));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == saleId);
        Assert.Equal(Seen, item.UnitPrice);
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_A_queued_cart_checkout_is_refused_when_the_catalog_price_moved()
    {
        var ctx = await SetupAsync(Seen);
        var code = await QueueAsync(ctx);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(new CheckoutCartCommand(code, Seen, 0, 0)
        {
            Items = [new CheckoutCartItemDto(ctx.VariantId, 1) { ExpectedUnitPrice = Seen }]
        }));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(Moved, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    // NARX-09
    [Fact]
    public async Task NARX_09_A_queued_cart_checkout_goes_through_when_the_price_still_matches()
    {
        var ctx = await SetupAsync(Seen);
        var code = await QueueAsync(ctx);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CheckoutCartCommand(code, Seen, 0, 0)
            {
                Items = [new CheckoutCartItemDto(ctx.VariantId, 1) { ExpectedUnitPrice = Seen }]
            })).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        Assert.Equal(Seen, sale.TotalAmount);
    }

    // NARX-10
    [Fact]
    public async Task NARX_10_Offline_replay_is_not_refused_when_the_catalog_price_moved()
    {
        var ctx = await SetupAsync(Seen);
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith([AppPermissions.Sales.Create, AppPermissions.Sales.Checkout]);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        var result = await TestOffline.PushAsync(Fixture, grant, TestOffline.Event(1, "sale.create",
            new CreateSaleRequest(ctx.Warehouse, null, Seen, 0, 0,
                [new CreateSaleItemRequest(ctx.VariantId, 1, Seen) { ExpectedUnitPrice = Seen }])
            { ApplyAutoDiscount = false }));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
    }

    // NARX-11
    [Fact]
    public async Task NARX_11_Every_changed_line_comes_back_in_one_refusal()
    {
        var ctx = await SetupAsync(Seen);
        var unit = await UnitAsync();
        var raised = await AddVariantAsync(ctx.Warehouse, unit, "Ikkinchi mahsulot", 20_000m);
        var untouched = await AddVariantAsync(ctx.Warehouse, unit, "Uchinchi mahsulot", 30_000m);

        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        await SetCatalogPriceAsync(raised, 25_000m);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(new CreateSaleCommand(ctx.Warehouse, null, 60_000m, 0, 0,
        [
            new CreateSaleItemDto(ctx.VariantId, 1, ExpectedUnitPrice: Seen),
            new CreateSaleItemDto(raised, 1, ExpectedUnitPrice: 20_000m),
            new CreateSaleItemDto(untouched, 1, ExpectedUnitPrice: 30_000m)
        ])
        { ApplyAutoDiscount = false }));

        Assert.Equal("price_changed", error.Code);
        var changes = Changes(error);
        Assert.Equal(2, changes.Count);

        var first = changes.Single(c => c.VariantId == ctx.VariantId);
        Assert.Equal(ProductName, first.ProductName);
        Assert.Equal(Seen, first.Expected);
        Assert.Equal(Moved, first.Current);

        var second = changes.Single(c => c.VariantId == raised);
        Assert.Equal("Ikkinchi mahsulot", second.ProductName);
        Assert.Equal(20_000m, second.Expected);
        Assert.Equal(25_000m, second.Current);

        Assert.DoesNotContain(changes, c => c.VariantId == untouched);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    // NARX-12
    [Fact]
    public async Task NARX_12_A_rate_move_refuses_the_sale_although_the_catalog_number_stands()
    {
        var ctx = await SetupAsync(Seen);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features.Where(f => f.Code == FeatureCatalog.Multicurrency
                    || f.Code == FeatureCatalog.PricingMulticurrency
                    || f.Code == FeatureCatalog.SalesMulticurrency)
                .ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, true).SetProperty(f => f.OwnerEnabled, true));
            await db.ProductPrices.Where(p => p.VariantId == ctx.VariantId)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.SellingPrice, 1m).SetProperty(p => p.Currency, "USD"));
        }
        await SetRateAsync("USD", Moved);

        var saleId = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: Moved));
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(Moved, (await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId)).TotalAmount);
        }

        await SetRateAsync("USD", 13_000m);

        var error = await RefusedAsync(x => x.Send(Sell(ctx, paid: Moved, expectedUnitPrice: Moved)));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(Moved, change.Expected);
        Assert.Equal(13_000m, change.Current);
        Assert.Equal(1m, await CatalogNumberAsync(ctx.VariantId));
    }
}
