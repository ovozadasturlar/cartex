using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
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
public class OfflineNegativeStockAuditTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal Price = 10_000m;
    private const decimal OnHand = 5m;
    private const decimal Shortfall = 37m;
    private const string OtherDevice = "another-pos";
    private const string Warning = "stock_negative_offline";
    private const string AuditEvent = "saleStockNegativeOffline";

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
            var productId = await sender.Send(new CreateProductCommand("Oflayn minus audit mahsuloti", null, unit, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
            await sender.Send(new CreateSupplyCommand(null, warehouse, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, OnHand, 6_000m, null, SellingPrice: Price)]));
        }

        var grant = await TestOffline.ClaimAsync(Fixture, warehouse);
        return new Ctx(branch, warehouse, variantId, grant);
    }

    private async Task MakeHeartbeatStaleAsync(long leaseId)
    {
        var moment = DateTime.UtcNow.AddMinutes(-2);
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

    private static IEnumerable<JsonElement> FindEvents(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (node.TryGetProperty("code", out var code)
                    && code.ValueKind == JsonValueKind.String && code.GetString() == AuditEvent)
                    yield return node;
                foreach (var found in node.EnumerateObject().SelectMany(p => FindEvents(p.Value)))
                    yield return found;
                break;
            case JsonValueKind.Array:
                foreach (var found in node.EnumerateArray().SelectMany(FindEvents))
                    yield return found;
                break;
        }
    }

    private async Task<List<JsonElement>> NegativeStockEventsAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var details = await db.AuditLogs.AsNoTracking().Where(a => a.Details != null)
            .Select(a => a.Details!).ToListAsync();
        return [.. details.SelectMany(d => FindEvents(JsonSerializer.Deserialize<JsonElement>(d)))];
    }

    private async Task<List<JsonElement>> NegativeStockRowsAsync()
    {
        var events = await NegativeStockEventsAsync();
        Assert.NotEmpty(events);
        return [.. events.SelectMany(e =>
            e.TryGetProperty("details", out var rows) && rows.ValueKind == JsonValueKind.Array
                ? rows.EnumerateArray()
                : [])];
    }

    private static decimal? Number(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    private static bool Describes(JsonElement row, Ctx ctx) =>
        Number(row, "WarehouseId") == ctx.Warehouse
        && Number(row, "VariantId") == ctx.VariantId
        && Number(row, "Shortfall") == Shortfall;

    // OFF-17
    [Fact]
    public async Task OFF_17_eased_sale_is_audited_with_warehouse_variant_and_shortfall()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var sale = await SellAsync(ctx, OnHand + Shortfall);
        Assert.Contains(Warning, sale.Warnings ?? []);

        Assert.Contains(await NegativeStockRowsAsync(), row => Describes(row, ctx));
    }

    // OFF-17
    [Fact]
    public async Task OFF_17_covered_sale_leaves_no_negative_stock_audit()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);
        await MakeHeartbeatStaleAsync(ctx.Grant.LeaseId);
        Fixture.CurrentUser.DeviceId = OtherDevice;

        var sale = await SellAsync(ctx, 1m);
        Assert.True(sale.Warnings is null or { Count: 0 });

        Assert.Empty(await NegativeStockEventsAsync());
    }

    // OFF-18, OFF-17
    [Fact]
    public async Task OFF_18_replayed_sale_goes_negative_and_is_audited()
    {
        var ctx = await SetupAsync();
        await SetPolicyAsync(allowInsufficientStock: false, allowNegativeWhenOffline: true);

        var quantity = OnHand + Shortfall;
        var result = await TestOffline.PushAsync(Fixture, ctx.Grant,
            TestOffline.Event(1, "sale.create", new CreateSaleRequest(ctx.Warehouse, null, quantity * Price, 0, 0,
                [new CreateSaleItemRequest(ctx.VariantId, quantity, Price)]) { ApplyAutoDiscount = false }));

        Assert.Equal("Applied", Assert.Single(result.Results).Status);
        Assert.Equal(-Shortfall, await StockAsync(ctx));

        Assert.Contains(await NegativeStockRowsAsync(), row => Describes(row, ctx));
    }
}
