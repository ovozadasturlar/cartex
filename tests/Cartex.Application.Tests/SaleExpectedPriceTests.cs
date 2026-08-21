using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Prepacks.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Rates.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
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
    private const decimal NeverListed = 7_777m;
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

    // NARX-14: narx tarixini chaqiruvchi kod emas, saqlash nuqtasi yozadi.
    private async Task SetCatalogPriceAsync(long variantId, decimal price, string? currency = null)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.ProductPrices.SingleAsync(p => p.VariantId == variantId);
        row.SellingPrice = price;
        if (currency is not null) row.Currency = currency;
        await db.SaveChangesAsync();
    }

    private async Task SetPolicyAsync(int driftWindowMinutes)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
        {
            MaxDiscountPercent = 100,
            UpdateCatalogPriceOnSale = true,
            MaxPriceIncreasePercent = null,
            PriceDriftWindowMinutes = driftWindowMinutes
        });
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

    // NARX-09: kassir ko'rgan narx savdo narxi bo'lib yoziladi — chegirma sifatida emas.
    private async Task AssertSoldAtAsync(long saleId, decimal price, decimal quantity = 1)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        var item = await db.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == saleId);
        Assert.Equal(price * quantity, sale.TotalAmount);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(price, item.UnitPrice);
    }

    private async Task<long> MaxHistoryIdAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ProductPriceHistory.AsNoTracking().Select(h => (long?)h.Id).MaxAsync() ?? 0;
    }

    private async Task<List<ProductPriceHistory>> HistoryAfterAsync(long variantId, long afterId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ProductPriceHistory.AsNoTracking()
            .Where(h => h.VariantId == variantId && h.Id > afterId)
            .OrderBy(h => h.Id)
            .ToListAsync();
    }

    [Fact]
    public async Task NARX_09_The_seen_price_wins_when_the_catalog_price_rose()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var before = await LedgerAsync(ctx);

        var saleId = await SellAsync(Sell(ctx, paid: Seen, expectedUnitPrice: Seen));

        await AssertSoldAtAsync(saleId, Seen);
        Assert.Equal(Moved, await CatalogNumberAsync(ctx.VariantId));
        Assert.Equal((before.Sales + 1, before.Stock - 1), await LedgerAsync(ctx));
    }

    [Fact]
    public async Task NARX_09_The_seen_price_wins_when_the_catalog_price_dropped()
    {
        var ctx = await SetupAsync(Moved);
        await SetPolicyAsync(60);
        await SetCatalogPriceAsync(ctx.VariantId, Seen);

        var saleId = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: Moved));

        await AssertSoldAtAsync(saleId, Moved);
        Assert.Equal(Seen, await CatalogNumberAsync(ctx.VariantId));
    }

    [Fact]
    public async Task NARX_10_An_accepted_drift_is_written_to_the_audit_trail()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        await SellAsync(Sell(ctx, paid: Seen, expectedUnitPrice: Seen));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AuditLogs.AsNoTracking().Select(a => new { a.Action, a.Summary, a.Details, a.NewData }).ToListAsync();
        var entries = logs.Select(a => string.Join('|', a.Action, a.Summary, a.Details, a.NewData));
        var drift = Assert.Single(entries, e => e.Contains("salePriceDrift"));
        Assert.Contains(ctx.VariantId.ToString(), drift);
        Assert.Contains("10000", drift);
        Assert.Contains("12000", drift);
    }

    [Fact]
    public async Task NARX_10_A_price_that_was_never_in_the_catalog_refuses_the_sale()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(Sell(ctx, paid: NeverListed, expectedUnitPrice: NeverListed)));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(ctx.VariantId, change.VariantId);
        Assert.Equal(ProductName, change.ProductName);
        Assert.Equal(NeverListed, change.Expected);
        Assert.Equal(Moved, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    [Fact]
    public async Task NARX_10_A_zero_window_refuses_a_price_that_has_only_just_left_the_catalog()
    {
        var ctx = await SetupAsync(Seen);
        await SetPolicyAsync(0);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(Sell(ctx, paid: Seen, expectedUnitPrice: Seen)));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(Seen, change.Expected);
        Assert.Equal(Moved, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    [Fact]
    public async Task NARX_10_A_drifted_sale_needs_no_price_override_permission()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith([AppPermissions.Sales.Create, AppPermissions.Sales.Checkout]);
        Assert.False(Fixture.CurrentUser.HasPermission(AppPermissions.Sales.PriceOverride));

        var saleId = await SellAsync(Sell(ctx, paid: Seen, expectedUnitPrice: Seen));

        await AssertSoldAtAsync(saleId, Seen);
    }

    [Fact]
    public async Task NARX_14_A_catalog_price_change_lands_in_history_with_its_own_window()
    {
        var ctx = await SetupAsync(Seen);
        var mark = await MaxHistoryIdAsync();

        var firstFrom = DateTime.UtcNow;
        await SetCatalogPriceAsync(ctx.VariantId, Moved);
        var firstTo = DateTime.UtcNow;

        var first = Assert.Single(await HistoryAfterAsync(ctx.VariantId, mark));
        Assert.Equal(Seen, first.SellingPrice);
        Assert.Equal("UZS", first.Currency);
        Assert.InRange(first.EffectiveTo, firstFrom.AddSeconds(-1), firstTo.AddSeconds(1));
        Assert.True(first.EffectiveFrom <= first.EffectiveTo,
            $"the old value must own a window, got {first.EffectiveFrom:O} .. {first.EffectiveTo:O}");
        Assert.True(first.EffectiveFrom <= firstFrom.AddSeconds(1),
            $"the old value was already in the catalog before the change, got {first.EffectiveFrom:O}");

        await Task.Delay(TimeSpan.FromSeconds(3));

        var secondFrom = DateTime.UtcNow;
        await SetCatalogPriceAsync(ctx.VariantId, 15_000m);
        var secondTo = DateTime.UtcNow;

        var second = Assert.Single(await HistoryAfterAsync(ctx.VariantId, first.Id));
        Assert.Equal(Moved, second.SellingPrice);
        Assert.InRange(second.EffectiveFrom, firstFrom.AddSeconds(-1), firstTo.AddSeconds(1));
        Assert.InRange(second.EffectiveTo, secondFrom.AddSeconds(-1), secondTo.AddSeconds(1));
    }

    [Fact]
    public async Task NARX_14_A_currency_change_lands_in_history_with_the_old_currency()
    {
        var ctx = await SetupAsync(Seen);
        var mark = await MaxHistoryIdAsync();

        await SetCatalogPriceAsync(ctx.VariantId, 1m, "USD");

        var row = Assert.Single(await HistoryAfterAsync(ctx.VariantId, mark));
        Assert.Equal(Seen, row.SellingPrice);
        Assert.Equal("UZS", row.Currency);
    }

    [Fact]
    public async Task NARX_11_Every_unrecognised_line_comes_back_in_one_refusal()
    {
        var ctx = await SetupAsync(Seen);
        var unit = await UnitAsync();
        var second = await AddVariantAsync(ctx.Warehouse, unit, "Ikkinchi mahsulot", 20_000m);
        var untouched = await AddVariantAsync(ctx.Warehouse, unit, "Uchinchi mahsulot", 30_000m);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(new CreateSaleCommand(ctx.Warehouse, null, 57_777m, 0, 0,
        [
            new CreateSaleItemDto(ctx.VariantId, 1, ExpectedUnitPrice: NeverListed),
            new CreateSaleItemDto(second, 1, ExpectedUnitPrice: 8_888m),
            new CreateSaleItemDto(untouched, 1, ExpectedUnitPrice: 30_000m)
        ])
        { ApplyAutoDiscount = false }));

        Assert.Equal("price_changed", error.Code);
        var changes = Changes(error);
        Assert.Equal(2, changes.Count);

        var firstChange = changes.Single(c => c.VariantId == ctx.VariantId);
        Assert.Equal(ProductName, firstChange.ProductName);
        Assert.Equal(NeverListed, firstChange.Expected);
        Assert.Equal(Seen, firstChange.Current);

        var secondChange = changes.Single(c => c.VariantId == second);
        Assert.Equal("Ikkinchi mahsulot", secondChange.ProductName);
        Assert.Equal(8_888m, secondChange.Expected);
        Assert.Equal(20_000m, secondChange.Current);

        Assert.DoesNotContain(changes, c => c.VariantId == untouched);
        Assert.Equal(before, await LedgerAsync(ctx));
    }

    [Fact]
    public async Task NARX_13_Offline_replay_is_never_refused_over_the_seen_price()
    {
        var ctx = await SetupAsync(Seen);
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith([AppPermissions.Sales.Create, AppPermissions.Sales.Checkout]);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        var result = await TestOffline.PushAsync(Fixture, grant, TestOffline.Event(1, "sale.create",
            new CreateSaleRequest(ctx.Warehouse, null, Seen, 0, 0,
                [new CreateSaleItemRequest(ctx.VariantId, 1, Seen) { ExpectedUnitPrice = NeverListed }])
            { ApplyAutoDiscount = false }));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
    }

    [Fact]
    public async Task NARX_13_A_prepack_line_is_not_checked_against_the_catalog_price()
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
            [new CreateSaleItemDto(ctx.VariantId, 1, PrepackId: prepackId, ExpectedUnitPrice: NeverListed)])
        { ApplyAutoDiscount = false });

        using var check = Fixture.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await context.Sales.AnyAsync(s => s.Id == saleId));
    }

    [Fact]
    public async Task NARX_13_Without_a_seen_price_the_check_is_skipped()
    {
        var ctx = await SetupAsync(Seen);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        var saleId = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: null));

        await AssertSoldAtAsync(saleId, Moved);
    }

    [Fact]
    public async Task NARX_12_A_rate_move_keeps_the_number_the_cashier_saw()
    {
        var ctx = await SetupAsync(Seen);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Features.Where(f => f.Code == FeatureCatalog.Multicurrency
                    || f.Code == FeatureCatalog.PricingMulticurrency
                    || f.Code == FeatureCatalog.SalesMulticurrency)
                .ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, true).SetProperty(f => f.OwnerEnabled, true));
        }

        await SetRateAsync("USD", Moved);
        await SetCatalogPriceAsync(ctx.VariantId, 1m, "USD");

        var atOldRate = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: Moved));
        await AssertSoldAtAsync(atOldRate, Moved);

        await SetRateAsync("USD", 13_000m);

        var afterRateMove = await SellAsync(Sell(ctx, paid: Moved, expectedUnitPrice: Moved));

        await AssertSoldAtAsync(afterRateMove, Moved);
        Assert.Equal(1m, await CatalogNumberAsync(ctx.VariantId));
    }

    [Fact]
    public async Task NARX_09_A_queued_cart_checkout_goes_through_at_the_price_the_cashier_saw()
    {
        var ctx = await SetupAsync(Seen);
        var code = await QueueAsync(ctx);
        await SetCatalogPriceAsync(ctx.VariantId, Moved);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CheckoutCartCommand(code, Seen, 0, 0)
            {
                Items = [new CheckoutCartItemDto(ctx.VariantId, 1) { ExpectedUnitPrice = Seen }]
            })).SaleId;
        }

        await AssertSoldAtAsync(saleId, Seen);
    }

    [Fact]
    public async Task NARX_10_A_queued_cart_checkout_is_refused_when_the_price_was_never_listed()
    {
        var ctx = await SetupAsync(Seen);
        var code = await QueueAsync(ctx);
        var before = await LedgerAsync(ctx);

        var error = await RefusedAsync(x => x.Send(new CheckoutCartCommand(code, NeverListed, 0, 0)
        {
            Items = [new CheckoutCartItemDto(ctx.VariantId, 1) { ExpectedUnitPrice = NeverListed }]
        }));

        Assert.Equal("price_changed", error.Code);
        var change = Assert.Single(Changes(error));
        Assert.Equal(NeverListed, change.Expected);
        Assert.Equal(Seen, change.Current);
        Assert.Equal(before, await LedgerAsync(ctx));
    }
}
