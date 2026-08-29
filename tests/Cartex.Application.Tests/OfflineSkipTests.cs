using Cartex.Application.Customers.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
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
public class OfflineSkipTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;

    private sealed record Ctx(long Branch, long Warehouse, long VariantId, long CustomerId, OfflineLeaseGrantDto Grant);

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

        long variantId, customerId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn skip mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 100, 6_000m, null, SellingPrice: Price)]));
            customerId = await sender.Send(new CreateCustomerCommand("Skip Mijoz",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m, CreditLimit: 10_000_000m));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(branch, warehouse, variantId, customerId, grant);
    }

    private static OfflineSyncEventRequest DebtSale(long sequence, long warehouse, long variantId, long? customerId) =>
        TestOffline.Event(sequence, "sale.create", new CreateSaleRequest(warehouse, customerId, 0, 0, 0,
            [new CreateSaleItemRequest(variantId, 1, Price)]) { ApplyAutoDiscount = false });

    private async Task<(int Sales, decimal Debt, decimal Stock)> SnapshotAsync(Ctx ctx)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sales = await db.Sales.CountAsync();
        var debt = (await db.Accounts.FirstOrDefaultAsync(a =>
            a.CustomerId == ctx.CustomerId && a.Type == AccountType.Debt))?.Balance ?? 0m;
        var stock = await db.Stocks.Where(s => s.VariantId == ctx.VariantId).SumAsync(s => s.Quantity);
        return (sales, debt, stock);
    }

    [Fact]
    public async Task OFF_03_Rejected_event_is_skipped_and_queue_continues()
    {
        var ctx = await SetupAsync();
        var e1 = DebtSale(1, ctx.Warehouse, ctx.VariantId, ctx.CustomerId);
        var e2 = DebtSale(2, ctx.Warehouse, ctx.VariantId, null);
        var e3 = DebtSale(3, ctx.Warehouse, ctx.VariantId, ctx.CustomerId);

        var first = await TestOffline.PushAsync(Fixture, ctx.Grant, e1);
        Assert.Equal("Applied", Assert.Single(first.Results).Status);

        var second = await TestOffline.PushAsync(Fixture, ctx.Grant, e2, e3);
        Assert.Equal("Rejected", second.Results[0].Status);
        Assert.Equal("Deferred", second.Results[1].Status);
        Assert.Equal(1, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));

        var before = await SnapshotAsync(ctx);
        var skipped = await TestOffline.SkipAsync(Fixture, ctx.Grant, e2, "kassir rad etdi");
        Assert.Equal("Skipped", skipped.Status);
        Assert.Equal(2, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));

        var after = await SnapshotAsync(ctx);
        Assert.Equal(before, after);

        var third = await TestOffline.PushAsync(Fixture, ctx.Grant, e3);
        Assert.Equal("Applied", Assert.Single(third.Results).Status);
        Assert.Equal(3, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
        Assert.Equal(before.Sales + 1, (await SnapshotAsync(ctx)).Sales);
    }

    [Fact]
    public async Task OFF_03_Skip_is_idempotent_by_event_id()
    {
        var ctx = await SetupAsync();
        var e1 = DebtSale(1, ctx.Warehouse, ctx.VariantId, ctx.CustomerId);
        var e2 = DebtSale(2, ctx.Warehouse, ctx.VariantId, null);

        await TestOffline.PushAsync(Fixture, ctx.Grant, e1);
        Assert.Equal("Rejected", Assert.Single((await TestOffline.PushAsync(Fixture, ctx.Grant, e2)).Results).Status);

        Assert.Equal("Skipped", (await TestOffline.SkipAsync(Fixture, ctx.Grant, e2)).Status);
        Assert.Equal(2, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));

        Assert.Equal("Skipped", (await TestOffline.SkipAsync(Fixture, ctx.Grant, e2)).Status);
        Assert.Equal(2, await TestOffline.LastAcceptedSequenceAsync(Fixture, ctx.Grant.LeaseId));
    }

    [Fact]
    public async Task OFF_03_Applied_event_cannot_be_skipped()
    {
        var ctx = await SetupAsync();
        var e1 = DebtSale(1, ctx.Warehouse, ctx.VariantId, ctx.CustomerId);
        Assert.Equal("Applied", Assert.Single((await TestOffline.PushAsync(Fixture, ctx.Grant, e1)).Results).Status);

        var error = await Assert.ThrowsAsync<ConflictException>(() => TestOffline.SkipAsync(Fixture, ctx.Grant, e1));
        Assert.Equal("offline_event_already_applied", error.Code);
    }

    [Fact]
    public async Task OFF_03_Skip_with_sequence_gap_is_refused()
    {
        var ctx = await SetupAsync();
        var e1 = DebtSale(1, ctx.Warehouse, ctx.VariantId, ctx.CustomerId);
        Assert.Equal("Applied", Assert.Single((await TestOffline.PushAsync(Fixture, ctx.Grant, e1)).Results).Status);

        var future = DebtSale(5, ctx.Warehouse, ctx.VariantId, null);
        var error = await Assert.ThrowsAnyAsync<DomainException>(() => TestOffline.SkipAsync(Fixture, ctx.Grant, future));
        Assert.Equal("offline_sequence_gap", error.Code);
    }
}
