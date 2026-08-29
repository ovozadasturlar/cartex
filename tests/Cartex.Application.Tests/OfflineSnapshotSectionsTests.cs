using Cartex.Application.Common.Messaging;
using Cartex.Application.Customers.Commands;
using Cartex.Application.OfflineCache.Queries;
using Cartex.Application.Products.Commands;
using Cartex.Application.Suppliers.Commands;
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
public class OfflineSnapshotSectionsTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<OfflineLeaseGrantDto> SetupAsync()
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

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn snapshot mahsulot", null, unit, 0,
                [new BarcodeInput("4780000000014")]));
            var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 50, 6_000m, null, SellingPrice: 10_000m)]));
            await sender.Send(new CreateCustomerCommand("Snapshot Mijoz",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 1_000_000m));
            await sender.Send(new CreateSupplierCommand("Snapshot Taminotchi", null));
        }

        return await TestOffline.ClaimAsync(Fixture, warehouse);
    }

    private async Task<OfflineSnapshotDto> SnapshotAsync(OfflineLeaseGrantDto grant,
        bool sales, bool payments, bool supplies)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetOfflineSnapshotQuery(grant.LeaseId, grant.Epoch, grant.LeaseToken,
            sales, payments, supplies));
    }

    [Fact]
    public async Task OFF_51_Payments_only_snapshot_carries_customers_only()
    {
        var grant = await SetupAsync();

        var snapshot = await SnapshotAsync(grant, sales: false, payments: true, supplies: false);

        Assert.NotEmpty(snapshot.Customers);
        Assert.Empty(snapshot.Products);
        Assert.Empty(snapshot.Barcodes);
        Assert.Empty(snapshot.Suppliers ?? []);
        Assert.Empty(snapshot.ParticipantRoles ?? []);
        Assert.Empty(snapshot.Partners ?? []);
    }

    [Fact]
    public async Task OFF_51_Supplies_only_snapshot_carries_products_barcodes_and_suppliers()
    {
        var grant = await SetupAsync();

        var snapshot = await SnapshotAsync(grant, sales: false, payments: false, supplies: true);

        Assert.NotEmpty(snapshot.Products);
        Assert.NotEmpty(snapshot.Barcodes);
        Assert.NotEmpty(snapshot.Suppliers ?? []);
        Assert.Empty(snapshot.Customers);
        Assert.Empty(snapshot.ParticipantRoles ?? []);
        Assert.Empty(snapshot.Partners ?? []);
    }

    [Fact]
    public async Task OFF_51_Default_snapshot_carries_all_sections()
    {
        var grant = await SetupAsync();

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var snapshot = await sender.Send(new GetOfflineSnapshotQuery(grant.LeaseId, grant.Epoch, grant.LeaseToken));

        Assert.NotEmpty(snapshot.Products);
        Assert.NotEmpty(snapshot.Barcodes);
        Assert.NotEmpty(snapshot.Customers);
        Assert.NotEmpty(snapshot.Suppliers ?? []);
    }
}
