using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineSplitBrainGuardTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const string OtherDevice = "another-pos";

    private sealed record Ctx(long AuthorityWarehouse, long OtherWarehouse, long VariantId, OfflineLeaseGrantDto Grant);

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
            var productId = await sender.Send(new CreateProductCommand("Split-brain mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10m, 6_000m, null, SellingPrice: Price)]));
            await sender.Send(new CreateSupplyCommand(null, otherWarehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10m, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(warehouse, otherWarehouse, variantId, grant);
    }

    private async Task MakeHeartbeatStaleAsync(long leaseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.OfflineAuthorityLeases.Where(x => x.Id == leaseId)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.LastHeartbeatAt, DateTime.UtcNow.AddMinutes(-2)));
    }

    private async Task SetPolicyAsync(bool allow)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowInsufficientStockSales = allow });
    }

    private async Task<long> SellOnlineAsync(long warehouseId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new CreateSaleCommand(warehouseId, null, Price, 0, 0,
            [new CreateSaleItemDto(variantId, 1)]) { ApplyAutoDiscount = false })).SaleId;
    }

    [Fact]
    public async Task OFF_15_Stale_holder_blocks_online_sale_from_authority_warehouse()
    {
        var ctx = await SetupAsync();
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var error = await Assert.ThrowsAsync<ConflictException>(() => SellOnlineAsync(ctx.AuthorityWarehouse, ctx.VariantId));
        Assert.Equal("offline_authority_possibly_active", error.Code);
    }

    [Fact]
    public async Task OFF_15_Stale_holder_does_not_block_online_sale_from_other_warehouse()
    {
        var ctx = await SetupAsync();
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var saleId = await SellOnlineAsync(ctx.OtherWarehouse, ctx.VariantId);
        Assert.True(saleId > 0);
    }

    [Fact]
    public async Task OFF_15_Insufficient_stock_policy_disables_the_block()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(true);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var saleId = await SellOnlineAsync(ctx.AuthorityWarehouse, ctx.VariantId);
        Assert.True(saleId > 0);
    }

    [Fact]
    public async Task OFF_15_Fresh_heartbeat_allows_online_sale_from_authority_warehouse()
    {
        var ctx = await SetupAsync();
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var saleId = await SellOnlineAsync(ctx.AuthorityWarehouse, ctx.VariantId);
        Assert.True(saleId > 0);
    }
}
