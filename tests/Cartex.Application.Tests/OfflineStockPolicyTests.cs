using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineStockPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const decimal OnHand = 10m;

    private sealed record Ctx(long Warehouse, long VariantId, OfflineLeaseGrantDto Grant);

    private async Task<Ctx> SetupAsync()
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
            var productId = await sender.Send(new CreateProductCommand("Oflayn zaxira mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, OnHand, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(warehouse, variantId, grant);
    }

    private async Task SetPolicyAsync(bool allow)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowInsufficientStockSales = allow });
    }

    private async Task<decimal> StockAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(s => s.VariantId == ctx.VariantId && s.WarehouseId == ctx.Warehouse)
            .SumAsync(s => s.Quantity);
    }

    private async Task<int> SalesCountAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Sales.CountAsync();
    }

    private static OfflineSyncEventRequest Oversell(Ctx ctx) =>
        TestOffline.Event(1, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, (OnHand + 2) * Price, 0, 0,
            [new CreateSaleItemRequest(ctx.VariantId, OnHand + 2, Price)]) { ApplyAutoDiscount = false });

    [Fact]
    public async Task OFF_14_Replay_oversell_goes_through_when_policy_allows()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(true);

        var result = await TestOffline.PushAsync(Fixture, ctx.Grant, Oversell(ctx));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);
        Assert.NotNull(applied.ResultEntityId);
        Assert.Equal(-2m, await StockAsync(ctx));
    }

    [Fact]
    public async Task OFF_14_Replay_oversell_is_rejected_when_policy_forbids()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(false);
        var before = await SalesCountAsync();

        var result = await TestOffline.PushAsync(Fixture, ctx.Grant, Oversell(ctx));

        Assert.Equal("Rejected", Assert.Single(result.Results).Status);
        Assert.Equal(OnHand, await StockAsync(ctx));
        Assert.Equal(before, await SalesCountAsync());
    }
}
