using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Supplies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class OfflineSupplyReplayTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Ctx(long Branch, long Warehouse, long VariantId, long SupplierId);

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

        long variantId, supplierId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand("Oflayn kirim mahsulot", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            var supplier = new Supplier { Name = "Oflayn Taminotchi" };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        return new Ctx(branch, warehouse, variantId, supplierId);
    }

    private static OfflineSyncEventRequest SupplyEvent(long sequence, Ctx ctx, decimal quantity, decimal purchasePrice,
        decimal paidCash = 0, string? currency = null, decimal? sellingPrice = null, string kind = "supply.create") =>
        TestOffline.Event(sequence, kind, new CreateSupplyRequest(ctx.SupplierId, ctx.Warehouse,
            DateOnly.FromDateTime(DateTime.Today),
            [new CreateSupplyItemRequest(ctx.VariantId, quantity, purchasePrice, null, SellingPrice: sellingPrice)],
            paidCash, 0, currency));

    private async Task<decimal> SupplierDebtAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Accounts.FirstOrDefaultAsync(a =>
            a.SupplierId == supplierId && a.Type == AccountType.Debt))?.Balance ?? 0m;
    }

    private async Task<int> SupplyCountAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Supplies.CountAsync(s => s.SupplierId == supplierId);
    }

    [Fact]
    public async Task OFF_30_Debt_only_supply_in_base_currency_is_applied()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant, SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m));

        var applied = Assert.Single(result.Results);
        Assert.Equal("Applied", applied.Status);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var supply = await db.Supplies.AsNoTracking().SingleAsync(s => s.Id == applied.ResultEntityId);
        Assert.Equal(10_000m, supply.TotalAmount);
        Assert.Equal(ctx.SupplierId, supply.SupplierId);
        var stock = await db.Stocks.Where(s => s.VariantId == ctx.VariantId && s.WarehouseId == ctx.Warehouse)
            .SumAsync(s => s.Quantity);
        Assert.Equal(5m, stock);
        Assert.Equal(-10_000m, await SupplierDebtAsync(ctx.SupplierId));
    }

    [Fact]
    public async Task OFF_30_Paid_supply_is_rejected_offline()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m, paidCash: 5_000m));

        var rejected = Assert.Single(result.Results);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("offline_supply_payment_unsupported", rejected.ErrorCode);
        Assert.Equal(0, await SupplyCountAsync(ctx.SupplierId));
        Assert.Equal(0m, await SupplierDebtAsync(ctx.SupplierId));
    }

    [Fact]
    public async Task OFF_30_Foreign_currency_supply_is_rejected_offline()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m, currency: "USD"));

        var rejected = Assert.Single(result.Results);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("offline_supply_currency_unsupported", rejected.ErrorCode);
        Assert.Equal(0, await SupplyCountAsync(ctx.SupplierId));
    }

    [Fact]
    public async Task OFF_31_Online_supply_with_same_idempotency_key_returns_existing_document()
    {
        var ctx = await SetupAsync();
        var key = $"supply-{Guid.NewGuid():N}";

        long firstId, secondId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            firstId = await sender.Send(new CreateSupplyCommand(ctx.SupplierId, ctx.Warehouse,
                DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(ctx.VariantId, 4, 3_000m, null)]) { IdempotencyKey = key });
        }
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            secondId = await sender.Send(new CreateSupplyCommand(ctx.SupplierId, ctx.Warehouse,
                DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(ctx.VariantId, 4, 3_000m, null)]) { IdempotencyKey = key });
        }

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, await SupplyCountAsync(ctx.SupplierId));
        Assert.Equal(-12_000m, await SupplierDebtAsync(ctx.SupplierId));
    }

    [Fact]
    public async Task OFF_31_Offline_supply_replay_is_idempotent_by_event_id()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);
        var supplyEvent = SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m);

        var first = await TestOffline.PushAsync(Fixture, grant, supplyEvent);
        var applied = Assert.Single(first.Results);
        Assert.Equal("Applied", applied.Status);

        var retry = await TestOffline.PushAsync(Fixture, grant, supplyEvent);
        var replayed = Assert.Single(retry.Results);
        Assert.Equal("AlreadyApplied", replayed.Status);
        Assert.Equal(applied.ResultEntityId, replayed.ResultEntityId);
        Assert.Equal(1, await SupplyCountAsync(ctx.SupplierId));
        Assert.Equal(-10_000m, await SupplierDebtAsync(ctx.SupplierId));
    }

    [Fact]
    public async Task OFF_32_Selling_price_in_offline_supply_updates_catalog()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m, sellingPrice: 15_000m));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var price = await db.ProductPrices.AsNoTracking().SingleAsync(p => p.VariantId == ctx.VariantId);
        Assert.Equal(15_000m, price.SellingPrice);
    }

    [Fact]
    public async Task OFF_02_Supply_kind_alias_normalizes_to_supply_create()
    {
        var ctx = await SetupAsync();
        var grant = await TestOffline.ClaimAsync(Fixture, ctx.Warehouse);

        var result = await TestOffline.PushAsync(Fixture, grant,
            SupplyEvent(1, ctx, quantity: 5, purchasePrice: 2_000m, kind: "supply"));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
        Assert.Equal(1, await SupplyCountAsync(ctx.SupplierId));
    }
}
