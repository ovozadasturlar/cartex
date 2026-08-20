using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.OfflineCache;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineNegativeStockPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const decimal OnHand = 10m;
    private const string OtherDevice = "another-pos";
    private const string Warning = "stock_negative_offline";

    private sealed record Ctx(long Branch, long Warehouse, long OtherWarehouse, long VariantId, OfflineLeaseGrantDto Grant);

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

        long otherWarehouse, variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var second = await db.Warehouses.FirstOrDefaultAsync(w => w.BranchId == branch && w.Id != warehouse);
            if (second is null)
            {
                second = new Warehouse { BranchId = branch, Name = "Ikkinchi ombor" };
                db.Warehouses.Add(second);
                await db.SaveChangesAsync();
            }
            otherWarehouse = second.Id;

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn minus qoldiq mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, OnHand, 6_000m, null, SellingPrice: Price)]));
            await sender.Send(new CreateSupplyCommand(null, otherWarehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, OnHand, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(branch, warehouse, otherWarehouse, variantId, grant);
    }

    private async Task MakeHeartbeatStaleAsync(long leaseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.OfflineAuthorityLeases.Where(x => x.Id == leaseId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.LastHeartbeatAt, DateTime.UtcNow.AddMinutes(-2)));
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

    private async Task GuardAsync(Ctx ctx, long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IOfflineAuthorityGuard>();
        await guard.EnsureOnlineMutationAllowedAsync(ctx.Branch, warehouseId, CancellationToken.None);
    }

    private async Task<CreateSaleResult> SellOnlineAsync(long warehouseId, long variantId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSaleCommand(warehouseId, null, quantity * Price, 0, 0,
            [new CreateSaleItemDto(variantId, quantity)]) { ApplyAutoDiscount = false });
    }

    private async Task<decimal> StockAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(s => s.VariantId == ctx.VariantId && s.WarehouseId == ctx.Warehouse)
            .SumAsync(s => s.Quantity);
    }

    private static OfflineSyncEventRequest Oversell(Ctx ctx) =>
        TestOffline.Event(1, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, (OnHand + 2) * Price, 0, 0,
            [new CreateSaleItemRequest(ctx.VariantId, OnHand + 2, Price)]) { ApplyAutoDiscount = false });

    // OFF-16
    [Fact]
    public async Task OFF_16_guard_does_not_block_when_setting_on()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        Assert.Null(await Record.ExceptionAsync(() => GuardAsync(ctx, ctx.Warehouse)));
    }

    // OFF-16
    [Fact]
    public async Task OFF_16_guard_still_blocks_when_setting_off()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: false);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var error = await Assert.ThrowsAsync<ConflictException>(() => GuardAsync(ctx, ctx.Warehouse));
        Assert.Equal("offline_authority_possibly_active", error.Code);
    }

    // OFF-16
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OFF_16_sale_goes_negative_only_inside_window(bool windowOpen)
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        if (windowOpen)
            await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        if (windowOpen)
        {
            var result = await SellOnlineAsync(ctx.Warehouse, ctx.VariantId, OnHand + 2);
            Assert.True(result.SaleId > 0);
            Assert.Equal(-2m, await StockAsync(ctx));
        }
        else
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => SellOnlineAsync(ctx.Warehouse, ctx.VariantId, OnHand + 2));
            Assert.Equal(OnHand, await StockAsync(ctx));
        }
    }

    // OFF-17
    [Fact]
    public async Task OFF_17_warning_returned()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var covered = await SellOnlineAsync(ctx.Warehouse, ctx.VariantId, 1m);
        Assert.True(covered.Warnings is null or { Count: 0 });

        var negative = await SellOnlineAsync(ctx.Warehouse, ctx.VariantId, OnHand + 2);
        Assert.Contains(Warning, negative.Warnings ?? []);
        Assert.Equal(-3m, await StockAsync(ctx));
    }

    // OFF-18
    [Theory]
    [InlineData(true, "Applied")]
    [InlineData(false, "Rejected")]
    public async Task OFF_18_replay_not_rejected(bool allowNegativeWhenOffline, string expected)
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: allowNegativeWhenOffline);

        var result = await TestOffline.PushAsync(Fixture, ctx.Grant, Oversell(ctx));

        Assert.Equal(expected, Assert.Single(result.Results).Status);
        Assert.Equal(allowNegativeWhenOffline ? -2m : OnHand, await StockAsync(ctx));
    }

    // OFF-16
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OFF_16_other_warehouse_never_blocked(bool allowNegativeWhenOffline)
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: allowNegativeWhenOffline);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        Assert.Null(await Record.ExceptionAsync(() => GuardAsync(ctx, ctx.OtherWarehouse)));

        var result = await SellOnlineAsync(ctx.OtherWarehouse, ctx.VariantId, 1m);
        Assert.True(result.SaleId > 0);
    }
}
