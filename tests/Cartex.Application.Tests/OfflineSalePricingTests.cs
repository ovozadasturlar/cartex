using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineSalePricingTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] CashierPermissions =
        ["sales.create", "sales.checkout", "sales.view", "customers.view", "rates.view"];

    private sealed record Ctx(long Branch, long Warehouse, long Business, long AdminId, long VariantId);

    private async Task<Ctx> SetupAsync(decimal capturedPrice)
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

        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn narx mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 50, 6_000m, null, SellingPrice: capturedPrice)]));
        }

        return new Ctx(branch, warehouse, business, admin, variantId);
    }

    private void AsSyncCashier(Ctx ctx)
    {
        Fixture.CurrentUser.AsCashier(ctx.AdminId, ctx.Business, ctx.Branch);
        Fixture.CurrentUser.Granted.UnionWith(CashierPermissions);
    }

    private async Task SetCatalogPriceAsync(long variantId, decimal price)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.ProductPrices.Where(p => p.VariantId == variantId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.SellingPrice, price));
    }

    private async Task<decimal> CatalogPriceAsync(long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.ProductPrices.AsNoTracking().SingleAsync(p => p.VariantId == variantId)).SellingPrice;
    }

    private async Task<(Sale Sale, SaleItem Item)> LoadSaleAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sale = await db.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        var item = await db.SaleItems.AsNoTracking().SingleAsync(i => i.SaleId == saleId);
        return (sale, item);
    }

    private static OfflineSyncEventRequest SaleEvent(long sequence, long warehouse, long variantId, decimal quantity,
        decimal unitPrice, decimal paidCash, decimal discountAmount = 0) =>
        TestOffline.Event(sequence, "sale.create", new CreateSaleRequest(warehouse, null, paidCash, 0, 0,
            [new CreateSaleItemRequest(variantId, quantity, unitPrice)])
        { ApplyAutoDiscount = false, DiscountAmount = discountAmount });

    [Fact]
    public async Task OFF_10_Server_price_rise_becomes_line_discount_at_replay()
    {
        var ctx = await SetupAsync(capturedPrice: 10_000m);
        AsSyncCashier(ctx);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        await SetCatalogPriceAsync(ctx.VariantId, 12_000m);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SaleEvent(1, ctx.Warehouse, ctx.VariantId, quantity: 2, unitPrice: 10_000m, paidCash: 20_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);
        var (sale, item) = await LoadSaleAsync(applied.ResultEntityId!.Value);
        Assert.Equal(20_000m, sale.TotalAmount);
        Assert.Equal(4_000m, sale.DiscountAmount);
        Assert.Equal(0m, sale.DebtAmount);
        Assert.Equal(0m, sale.ChangeAmount);
        Assert.Equal(12_000m, item.UnitPrice);
        Assert.Equal(4_000m, item.DiscountAmount);
        Assert.Equal(12_000m, await CatalogPriceAsync(ctx.VariantId));
    }

    [Fact]
    public async Task OFF_10_Server_price_drop_replays_at_captured_price()
    {
        var ctx = await SetupAsync(capturedPrice: 12_000m);
        AsSyncCashier(ctx);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        await SetCatalogPriceAsync(ctx.VariantId, 10_000m);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SaleEvent(1, ctx.Warehouse, ctx.VariantId, quantity: 1, unitPrice: 12_000m, paidCash: 12_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);
        var (sale, item) = await LoadSaleAsync(applied.ResultEntityId!.Value);
        Assert.Equal(12_000m, sale.TotalAmount);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(12_000m, item.UnitPrice);
    }

    [Fact]
    public async Task OFF_11_Replay_never_updates_catalog_and_logs_skipped_increase()
    {
        var ctx = await SetupAsync(capturedPrice: 12_000m);
        AsSyncCashier(ctx);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        await SetCatalogPriceAsync(ctx.VariantId, 10_000m);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SaleEvent(1, ctx.Warehouse, ctx.VariantId, quantity: 1, unitPrice: 12_000m, paidCash: 12_000m));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
        Assert.Equal(10_000m, await CatalogPriceAsync(ctx.VariantId));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logs = await db.AuditLogs.AsNoTracking().ToListAsync();
        Assert.Contains(logs, a =>
            a.Action.Contains("salePriceUpSkipped") || (a.Details != null && a.Details.Contains("salePriceUpSkipped")));
    }

    [Fact]
    public async Task OFF_10_Manual_discount_in_payload_is_preauthorized()
    {
        var ctx = await SetupAsync(capturedPrice: 10_000m);
        AsSyncCashier(ctx);
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SaleEvent(1, ctx.Warehouse, ctx.VariantId, quantity: 1, unitPrice: 10_000m, paidCash: 8_000m,
                discountAmount: 2_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);
        var (sale, _) = await LoadSaleAsync(applied.ResultEntityId!.Value);
        Assert.Equal(8_000m, sale.TotalAmount);
        Assert.Equal(2_000m, sale.DiscountAmount);
    }

    [Fact]
    public async Task OFF_12_Auto_discount_is_not_applied_at_replay()
    {
        var ctx = await SetupAsync(capturedPrice: 10_000m);

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DiscountRules.Add(new DiscountRule
            {
                Name = "Oflayn aksiya",
                IsEnabled = true,
                Scope = DiscountScope.All,
                Method = DiscountMethod.Percent,
                Value = 10m
            });
            await db.SaveChangesAsync();
        }

        long controlSaleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            controlSaleId = (await sender.Send(new CreateSaleCommand(ctx.Warehouse, null, 10_000m, 0, 0,
                [new CreateSaleItemDto(ctx.VariantId, 1)]))).SaleId;
        }
        var (controlSale, _) = await LoadSaleAsync(controlSaleId);
        Assert.True(controlSale.DiscountAmount > 0, "arrangement: the rule must discount an online sale");

        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        var result = await TestOffline.PushAsync(Fixture, grant,
            SaleEvent(1, ctx.Warehouse, ctx.VariantId, quantity: 1, unitPrice: 10_000m, paidCash: 10_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);
        var (sale, _) = await LoadSaleAsync(applied.ResultEntityId!.Value);
        Assert.Equal(0m, sale.DiscountAmount);
        Assert.Equal(10_000m, sale.TotalAmount);
    }
}
