using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.OfflineCache;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineAbandonedLeaseTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const decimal OnHand = 10m;
    private const string OtherDevice = "another-pos";
    private const string Block = "offline_authority_possibly_active";
    private const int InsideWindowMinutes = 23 * 60;
    private const int AbandonedMinutes = 24 * 60 + 1;

    private sealed record Ctx(long Branch, long Warehouse, long VariantId, OfflineLeaseGrantDto Grant);

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
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var productId = await sender.Send(new CreateProductCommand("Tashlab ketilgan vakolat mahsuloti", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, OnHand, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(branch, warehouse, variantId, grant);
    }

    private async Task SetHeartbeatAgeAsync(long leaseId, int minutes)
    {
        var moment = DateTime.UtcNow.AddMinutes(-minutes);
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.OfflineAuthorityLeases.Where(x => x.Id == leaseId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.LastHeartbeatAt, moment));
    }

    private async Task SetPolicyAsync(bool allowInsufficientStock, bool allowNegativeWhenOffline)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
        {
            AllowInsufficientStockSales = allowInsufficientStock,
            AllowNegativeStockWhenOffline = allowNegativeWhenOffline
        });
    }

    private async Task GuardAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IOfflineAuthorityGuard>();
        await guard.EnsureOnlineMutationAllowedAsync(ctx.Branch, ctx.Warehouse, CancellationToken.None);
    }

    private async Task<CreateSaleResult> SellAsync(Ctx ctx, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSaleCommand(ctx.Warehouse, null, quantity * Price, 0, 0,
            [new CreateSaleItemDto(ctx.VariantId, quantity)]) { ApplyAutoDiscount = false });
    }

    private async Task<decimal> StockAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(s => s.VariantId == ctx.VariantId && s.WarehouseId == ctx.Warehouse)
            .SumAsync(s => s.Quantity);
    }

    // OFF-19
    [Theory]
    [InlineData(InsideWindowMinutes, true)]
    [InlineData(AbandonedMinutes, false)]
    public async Task OFF_19_split_brain_block_stops_after_24_hours(int heartbeatAgeMinutes, bool windowOpen)
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: false);
        await SetHeartbeatAgeAsync(ctx.Grant.LeaseId, heartbeatAgeMinutes);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        if (windowOpen)
        {
            var error = await Assert.ThrowsAsync<ConflictException>(() => GuardAsync(ctx));
            Assert.Equal(Block, error.Code);
        }
        else
        {
            Assert.Null(await Record.ExceptionAsync(() => GuardAsync(ctx)));
            var sale = await SellAsync(ctx, 1m);
            Assert.True(sale.SaleId > 0);
            Assert.Equal(OnHand - 1m, await StockAsync(ctx));
        }
    }

    // OFF-19
    [Theory]
    [InlineData(InsideWindowMinutes, true)]
    [InlineData(AbandonedMinutes, false)]
    public async Task OFF_19_negative_stock_easing_stops_after_24_hours(int heartbeatAgeMinutes, bool windowOpen)
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        await SetHeartbeatAgeAsync(ctx.Grant.LeaseId, heartbeatAgeMinutes);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        if (windowOpen)
        {
            var sale = await SellAsync(ctx, OnHand + 2m);
            Assert.True(sale.SaleId > 0);
            Assert.Equal(-2m, await StockAsync(ctx));
        }
        else
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => SellAsync(ctx, OnHand + 2m));
            Assert.Equal(OnHand, await StockAsync(ctx));
        }
    }
}
