using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Ordering.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md NAVBAT-06, NAVBAT-07 and SOZ-13. The queue is how a shop
/// chooses to work, so it is a policy switch; a proforma is paper, not a queue entry.
[Collection("database")]
public class QueuePolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long VariantId);

    private async Task<Setup> SetupAsync(bool allowQueue)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowSaleQueue = allowQueue });

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variantId = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .OrderBy(x => x)
            .FirstAsync();

        return new Setup(warehouse, variantId);
    }

    private static SubmitCartCommand Cart(Setup s, CartKind? kind = null) =>
        new(s.Warehouse, null, [new SubmitCartItemDto(s.VariantId, 1)]) { Kind = kind };

    [Fact]
    public async Task NAVBAT_06_A_shop_that_turned_the_queue_off_cannot_queue_a_cart()
    {
        var s = await SetupAsync(allowQueue: false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Cart(s)));

        Assert.True(error is BusinessRuleException { Code: "sale_queue_disabled" },
            $"expected sale_queue_disabled, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task NAVBAT_06_The_queue_works_by_default()
    {
        var s = await SetupAsync(allowQueue: true);

        using var scope = Fixture.CreateScope();
        var code = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Cart(s));

        Assert.False(string.IsNullOrWhiteSpace(code));
    }

    [Fact]
    public async Task NAVBAT_07_A_proforma_cart_is_saved_but_never_shows_up_in_the_queue()
    {
        var s = await SetupAsync(allowQueue: true);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var code = await sender.Send(Cart(s, CartKind.Proforma));

        // Saqlanadi — aks holda server proformani chiqara olmaydi.
        Assert.True(await db.Carts.AsNoTracking().AnyAsync(x => x.AggregateCode == code));

        var queue = await sender.Send(new GetCartsQuery("Open", null, "Queue"));
        Assert.DoesNotContain(queue, x => x.AggregateCode == code);
    }

    [Fact]
    public async Task NAVBAT_07_A_proforma_is_still_allowed_when_the_queue_is_off()
    {
        var s = await SetupAsync(allowQueue: false);

        using var scope = Fixture.CreateScope();
        var code = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(Cart(s, CartKind.Proforma));

        Assert.False(string.IsNullOrWhiteSpace(code));
    }
}
