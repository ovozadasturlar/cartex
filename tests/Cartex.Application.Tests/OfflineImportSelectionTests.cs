using Cartex.Application.Common.Messaging;
using Cartex.Application.OfflineCache.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineImportSelectionTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const string RescueDevice = "rescue-device";

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
            var productId = await sender.Send(new CreateProductCommand("Oflayn tanlov mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 100, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(warehouse, variantId, grant);
    }

    private static OfflineSyncEventRequest CashSale(long sequence, Ctx ctx) =>
        TestOffline.Event(sequence, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, Price, 0, 0,
            [new CreateSaleItemRequest(ctx.VariantId, 1, Price)]) { ApplyAutoDiscount = false });

    private async Task<OfflineSyncBatchResult> ImportAsync(Ctx ctx, IReadOnlyList<Guid> skipEventIds,
        params OfflineSyncEventRequest[] events)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new ImportOfflineSyncCommand(ctx.Grant.LeaseId, ctx.Grant.Epoch, ctx.Grant.LeaseToken,
            events, false, skipEventIds));
    }

    private async Task<int> SalesCountAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Sales.CountAsync();
    }

    private async Task<string?> EventStatusAsync(Guid eventId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.OfflineSyncEvents.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId))?.Status;
    }

    [Fact]
    public async Task OFF_44_Selected_out_event_is_recorded_skipped_and_the_rest_apply()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        var e1 = CashSale(1, ctx);
        var e2 = CashSale(2, ctx);
        var e3 = CashSale(3, ctx);
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await ImportAsync(ctx, [e2.EventId], e1, e2, e3);

        Assert.Equal("Applied", result.Results[0].Status);
        Assert.Equal("Skipped", result.Results[1].Status);
        Assert.Equal("Applied", result.Results[2].Status);
        Assert.Equal("Skipped", await EventStatusAsync(e2.EventId));
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
        Assert.Equal(before + 2, await SalesCountAsync());
    }

    [Fact]
    public async Task OFF_44_Repaired_device_cannot_recreate_a_selected_out_event()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        var e1 = CashSale(1, ctx);
        var e2 = CashSale(2, ctx);
        var e3 = CashSale(3, ctx);
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var import = await ImportAsync(ctx, [e2.EventId], e1, e2, e3);
        Assert.Equal("Skipped", import.Results[1].Status);
        Assert.Equal(before + 2, await SalesCountAsync());

        Fixture.CurrentUser.DeviceId = TestOffline.Device;
        var push = await TestOffline.PushAsync(Fixture, ctx.Grant, e2);

        Assert.Equal("AlreadyApplied", Assert.Single(push.Results).Status);
        Assert.Equal(before + 2, await SalesCountAsync());
        Assert.Equal("Skipped", await EventStatusAsync(e2.EventId));
    }

    [Fact]
    public async Task OFF_44_Valid_event_in_the_skip_list_is_skipped_not_applied()
    {
        var ctx = await SetupAsync();
        var before = await SalesCountAsync();
        var e1 = CashSale(1, ctx);
        Fixture.CurrentUser.DeviceId = RescueDevice;

        var result = await ImportAsync(ctx, [e1.EventId], e1);

        Assert.Equal("Skipped", Assert.Single(result.Results).Status);
        Assert.Equal("Skipped", await EventStatusAsync(e1.EventId));
        Assert.Equal(1, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
        Assert.Equal(before, await SalesCountAsync());
    }
}
