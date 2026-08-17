using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class CatalogPriceUpdateTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Catalog = 100_000m;
    private const decimal Quantity = 2m;

    private sealed record Setup(long Branch, long Warehouse, long Business, long Admin, long Variant);

    /// One stocked variant priced at exactly 100 000 in the base currency, with no warehouse row left,
    /// so NARX-01 resolves to the worked example of the NARX-07 criterion.
    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = await db.Businesses.FirstAsync();
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var product = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variant = (await db.ProductVariants.FirstAsync(v => v.ProductId == product)).Id;

        var prices = await db.ProductPrices.Where(p => p.VariantId == variant).ToListAsync();
        db.ProductPrices.RemoveRange(prices.Where(p => p.WarehouseId != null));
        var general = prices.Single(p => p.WarehouseId == null);
        general.SellingPrice = Catalog;
        general.Currency = business.Currency;
        await db.SaveChangesAsync();

        var onHand = await db.Stocks.Where(s => s.VariantId == variant && s.WarehouseId == warehouse).SumAsync(s => s.Quantity);
        Assert.True(onHand >= Quantity, $"fixture needs at least {Quantity} on hand, found {onHand}");

        return new Setup(branch, warehouse, business.Id, admin, variant);
    }

    private async Task SellingAsAdminAsync(Setup s, bool updateCatalog, decimal? maxIncreasePercent)
    {
        using (var scope = Fixture.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
            {
                MaxDiscountPercent = 100,
                UpdateCatalogPriceOnSale = updateCatalog,
                MaxPriceIncreasePercent = maxIncreasePercent
            });
        }

        Fixture.CurrentUser.AsAdmin(s.Admin, s.Business, s.Branch);
        await TestShift.OpenAsync(Fixture);
    }

    private async Task<long> SellAsync(Setup s, decimal enteredPrice, decimal paid)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new CreateSaleCommand(s.Warehouse, null, paid, 0, 0,
            [new CreateSaleItemDto(s.Variant, Quantity, enteredPrice)])
        {
            ApplyAutoDiscount = false
        })).SaleId;
    }

    /// NARX-01: the warehouse row wins, the general row is the fallback. Read back in a fresh scope
    /// so a write-back that lands on either row is caught.
    private async Task<decimal> CatalogPriceAsync(Setup s)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var prices = await db.ProductPrices
            .Where(p => p.VariantId == s.Variant && (p.WarehouseId == null || p.WarehouseId == s.Warehouse))
            .ToListAsync();
        return (prices.FirstOrDefault(p => p.WarehouseId == s.Warehouse)
                ?? prices.Single(p => p.WarehouseId == null)).SellingPrice;
    }

    private async Task AssertSaleAsync(long saleId, decimal unitPrice, decimal discount, decimal total)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.Include(x => x.Items).SingleAsync(x => x.Id == saleId);

        Assert.Equal(Quantity, sale.Items.Sum(i => i.Quantity));
        Assert.All(sale.Items, i => Assert.Equal(unitPrice, i.UnitPrice));
        Assert.Equal(discount, sale.DiscountAmount);
        Assert.Equal(total, sale.TotalAmount);
    }

    [Fact]
    public async Task NARX_07_Increase_within_the_limit_updates_the_catalog()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: true, maxIncreasePercent: 10m);

        // 100 000 -> 105 000 is +5%, inside the 10% ceiling. Sale: 2 x 105 000 = 210 000, no discount.
        var saleId = await SellAsync(s, 105_000m, 210_000m);

        await AssertSaleAsync(saleId, unitPrice: 105_000m, discount: 0m, total: 210_000m);
        Assert.Equal(105_000m, await CatalogPriceAsync(s));
    }

    [Fact]
    public async Task NARX_07_Increase_above_the_limit_leaves_the_catalog_but_the_sale_goes_through()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: true, maxIncreasePercent: 10m);

        // 100 000 -> 130 000 is +30%, over the 10% ceiling. Sale still 2 x 130 000 = 260 000 (NARX-03),
        // catalogue holds at 100 000.
        var saleId = await SellAsync(s, 130_000m, 260_000m);

        await AssertSaleAsync(saleId, unitPrice: 130_000m, discount: 0m, total: 260_000m);
        Assert.Equal(Catalog, await CatalogPriceAsync(s));
    }

    [Fact]
    public async Task NARX_06_Catalog_is_never_updated_when_the_setting_is_off()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: false, maxIncreasePercent: 10m);

        // 100 000 -> 101 000 is +1%, well inside the 10% ceiling, so only the switch can hold the catalogue.
        // Sale: 2 x 101 000 = 202 000.
        var saleId = await SellAsync(s, 101_000m, 202_000m);

        await AssertSaleAsync(saleId, unitPrice: 101_000m, discount: 0m, total: 202_000m);
        Assert.Equal(Catalog, await CatalogPriceAsync(s));
    }

    [Fact]
    public async Task NARX_07_An_empty_ceiling_lets_a_large_increase_reach_the_catalogue()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: true, maxIncreasePercent: null);

        // SOZ-02: an empty ceiling is "no limit". 100 000 -> 130 000 (+30%) must reach the
        // catalogue. Sale: 2 x 130 000 = 260 000.
        var saleId = await SellAsync(s, 130_000m, 260_000m);

        await AssertSaleAsync(saleId, unitPrice: 130_000m, discount: 0m, total: 260_000m);
        Assert.Equal(130_000m, await CatalogPriceAsync(s));
    }

    /// SOZ-02: a zero ceiling now means the catalogue is never raised from a sale.
    [Fact]
    public async Task NARX_07_A_zero_ceiling_keeps_the_catalogue_untouched()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: true, maxIncreasePercent: 0m);

        var saleId = await SellAsync(s, 130_000m, 260_000m);

        await AssertSaleAsync(saleId, unitPrice: 130_000m, discount: 0m, total: 260_000m);
        Assert.Equal(Catalog, await CatalogPriceAsync(s));
    }

    [Fact]
    public async Task NARX_02_Lowered_price_is_a_discount_and_never_reaches_the_catalog()
    {
        var s = await SetupAsync();
        await SellingAsAdminAsync(s, updateCatalog: true, maxIncreasePercent: 10m);

        // 100 000 -> 90 000 is a price cut, not a catalogue change, even though 10% is inside the ceiling.
        // NARX-02: stored UnitPrice stays the catalogue 100 000, discount is 10 000 x 2 = 20 000.
        // Total: 2 x 100 000 - 20 000 = 180 000.
        var saleId = await SellAsync(s, 90_000m, 180_000m);

        await AssertSaleAsync(saleId, unitPrice: Catalog, discount: 20_000m, total: 180_000m);
        Assert.Equal(Catalog, await CatalogPriceAsync(s));
    }
}
