using Cartex.Application.Common.Messaging;
using Cartex.Application.Customers.Commands;
using Cartex.Application.OfflineCache.Queries;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineSnapshotDeltaTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal PriceA = 10_000m;
    private const decimal PriceB = 20_000m;

    private sealed record Setup(
        long Warehouse,
        long ProductA,
        long VariantA,
        long VariantB,
        long CustomerA,
        long CustomerB,
        OfflineLeaseGrantDto Grant);

    private async Task<Setup> SetupAsync()
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

        long productA, variantA, variantB, customerA, customerB;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            productA = await sender.Send(new CreateProductCommand("Delta mahsulot A", null, unit, 0,
                [new BarcodeInput("4780000000021")], SellingPrice: PriceA));
            variantA = (await db.ProductVariants.FirstAsync(v => v.ProductId == productA)).Id;

            var productB = await sender.Send(new CreateProductCommand("Delta mahsulot B", null, unit, 0,
                [new BarcodeInput("4780000000038")], SellingPrice: PriceB));
            variantB = (await db.ProductVariants.FirstAsync(v => v.ProductId == productB)).Id;

            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantA, 50, 6_000m, null), new CreateSupplyItemDto(variantB, 50, 12_000m, null)]));

            customerA = await sender.Send(new CreateCustomerCommand("Delta Mijoz A",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
            customerB = await sender.Send(new CreateCustomerCommand("Delta Mijoz B",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Setup(warehouse, productA, variantA, variantB, customerA, customerB, grant);
    }

    /// OFF-54(b): the server widens the delta window by 5 seconds, so rows written moments before the
    /// baseline snapshot legitimately come back. Tests that assert "only the changed row" must first
    /// let the fixture writes fall outside that window.
    private static Task SettleAsync() => Task.Delay(TimeSpan.FromSeconds(6));

    private async Task<OfflineSnapshotDto> SnapshotAsync(OfflineLeaseGrantDto grant, DateTime? since = null)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetOfflineSnapshotQuery(grant.LeaseId, grant.Epoch, grant.LeaseToken, Since: since));
    }

    private async Task SetPriceAsync(Setup s, long variantId, decimal price)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new SetProductPriceCommand(variantId, s.Warehouse, price));
    }

    private async Task SupplyAsync(Setup s, long variantId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CreateSupplyCommand(null, s.Warehouse, DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemDto(variantId, quantity, 6_000m, null)]));
    }

    private async Task WithdrawProductAsync(long productId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new SetProductStateCommand(productId, false));
    }

    private async Task CreditSaleAsync(Setup s, long variantId, long customerId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CreateSaleCommand(s.Warehouse, customerId, 0, 0, 0,
            [new CreateSaleItemDto(variantId, quantity)]));
    }

    // OFF-53
    [Fact]
    public async Task OFF_53_first_pull_without_since_is_full()
    {
        var s = await SetupAsync();

        var full = await SnapshotAsync(s.Grant);

        Assert.True(full.IsFull);
        Assert.Contains(full.Products, p => p.VariantId == s.VariantA);
        Assert.Contains(full.Products, p => p.VariantId == s.VariantB);
        Assert.Equal(full.Products.Count, full.Totals.Products);
        Assert.Equal(full.Barcodes.Count, full.Totals.Barcodes);
        Assert.Equal(full.Customers.Count, full.Totals.Customers);
        Assert.Equal((full.Suppliers ?? []).Count, full.Totals.Suppliers);
        Assert.Empty(full.RemovedProductIds);
        Assert.Empty(full.RemovedCustomerIds);
        Assert.Empty(full.RemovedSupplierIds);
        Assert.Empty(full.RemovedBarcodeCodes);
    }

    // OFF-53
    [Fact]
    public async Task OFF_53_delta_returns_only_changed()
    {
        var s = await SetupAsync();
        await SettleAsync();
        var full = await SnapshotAsync(s.Grant);

        await SetPriceAsync(s, s.VariantA, 12_500m);
        var delta = await SnapshotAsync(s.Grant, full.ServerTime);

        Assert.False(delta.IsFull);
        Assert.Equal([s.VariantA], delta.Products.Select(p => p.VariantId).ToArray());
        Assert.Equal(12_500m, delta.Products.Single().SellingPrice);
    }

    // OFF-54
    [Fact]
    public async Task OFF_54_stock_change_marks_product_changed()
    {
        var s = await SetupAsync();
        await SettleAsync();
        var full = await SnapshotAsync(s.Grant);
        var before = full.Products.Single(p => p.VariantId == s.VariantB);

        await SupplyAsync(s, s.VariantB, 7m);
        var delta = await SnapshotAsync(s.Grant, full.ServerTime);

        Assert.False(delta.IsFull);
        Assert.Contains(delta.Products, p => p.VariantId == s.VariantB);
        Assert.DoesNotContain(delta.Products, p => p.VariantId == s.VariantA);
        Assert.Equal(before.Quantity + 7m, delta.Products.Single(p => p.VariantId == s.VariantB).Quantity);
    }

    /// The cached product rows are keyed by VariantId, so the eviction list has to carry the same key
    /// the client stores — otherwise the client cannot find the row it must drop.
    // OFF-53
    [Fact]
    public async Task OFF_53_removed_products_are_listed()
    {
        var s = await SetupAsync();
        await SettleAsync();
        var full = await SnapshotAsync(s.Grant);
        Assert.Contains(full.Products, p => p.VariantId == s.VariantA);

        await WithdrawProductAsync(s.ProductA);
        var delta = await SnapshotAsync(s.Grant, full.ServerTime);

        Assert.False(delta.IsFull);
        Assert.DoesNotContain(delta.Products, p => p.VariantId == s.VariantA);
        Assert.Contains(s.VariantA, delta.RemovedProductIds);
    }

    // OFF-55
    [Fact]
    public async Task OFF_55_stale_since_forces_full()
    {
        var s = await SetupAsync();
        var full = await SnapshotAsync(s.Grant);

        var stale = await SnapshotAsync(s.Grant, full.ServerTime.AddDays(-8));

        Assert.True(stale.IsFull);
        Assert.Contains(stale.Products, p => p.VariantId == s.VariantA);
        Assert.Contains(stale.Products, p => p.VariantId == s.VariantB);
        Assert.Empty(stale.RemovedProductIds);
    }

    // OFF-54
    [Fact]
    public async Task OFF_54_totals_match_full_snapshot()
    {
        var s = await SetupAsync();
        await SettleAsync();
        var full = await SnapshotAsync(s.Grant);

        await SetPriceAsync(s, s.VariantA, 13_000m);
        var delta = await SnapshotAsync(s.Grant, full.ServerTime);

        Assert.False(delta.IsFull);
        Assert.Single(delta.Products);
        Assert.Equal(full.Totals, delta.Totals);
        Assert.True(delta.Totals.Products > delta.Products.Count,
            "totals must count the whole cache, not the changed rows");
    }

    // OFF-53
    [Fact]
    public async Task OFF_53_customer_delta()
    {
        var s = await SetupAsync();
        await SettleAsync();
        var full = await SnapshotAsync(s.Grant);
        Assert.Equal(0m, full.Customers.Single(c => c.Id == s.CustomerA).DebtBalance);

        await CreditSaleAsync(s, s.VariantB, s.CustomerA, 2m);
        var delta = await SnapshotAsync(s.Grant, full.ServerTime);

        Assert.False(delta.IsFull);
        Assert.Contains(delta.Customers, c => c.Id == s.CustomerA);
        Assert.DoesNotContain(delta.Customers, c => c.Id == s.CustomerB);
        Assert.Equal(2m * PriceB, delta.Customers.Single(c => c.Id == s.CustomerA).DebtBalance);
    }
}
