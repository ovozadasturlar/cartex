using Cartex.Application.Common.Messaging;
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

[Collection("database")]
public class CartKindTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<long> LoginAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch);
        return warehouse;
    }

    private static async Task<string> SubmitAsync(IServiceScope scope, long warehouseId, CartKind? kind = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var variantId = await db.ProductVariants.Select(v => v.Id).FirstAsync();
        return await sender.Send(new SubmitCartCommand(warehouseId, null,
            [new SubmitCartItemDto(variantId, 1m)], Kind: kind));
    }

    [Fact]
    public async Task Submit_DefaultsToQueue()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var code = await SubmitAsync(scope, warehouseId);

        var cart = await db.Carts.SingleAsync(c => c.AggregateCode == code);
        Assert.Equal(CartKind.Queue, cart.Kind);
    }

    [Fact]
    public async Task Submit_ExplicitKind_Wins()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var code = await SubmitAsync(scope, warehouseId, CartKind.Order);

        var cart = await db.Carts.SingleAsync(c => c.AggregateCode == code);
        Assert.Equal(CartKind.Order, cart.Kind);
    }

    [Fact]
    public async Task Submit_UserDestination_Overrides()
    {
        var warehouseId = await LoginAsync();
        using var setup = Fixture.CreateScope();
        var setupDb = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await setupDb.Users.FirstAsync(u => u.Username == "admin");
        admin.CartDestination = "order";
        await setupDb.SaveChangesAsync();

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await SubmitAsync(scope, warehouseId);

        var cart = await db.Carts.SingleAsync(c => c.AggregateCode == code);
        Assert.Equal(CartKind.Order, cart.Kind);

        admin.CartDestination = null;
        await setupDb.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCarts_FiltersByKind()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await SubmitAsync(scope, warehouseId);
        var orderCode = await SubmitAsync(scope, warehouseId, CartKind.Order);

        var result = await sender.Send(new GetCartsQuery(Kind: "Order"));

        var cart = Assert.Single(result);
        Assert.Equal(orderCode, cart.AggregateCode);
    }

    [Fact]
    public async Task Claim_IsAtomic()
    {
        var warehouseId = await LoginAsync();
        string code;
        using (var scope = Fixture.CreateScope())
            code = await SubmitAsync(scope, warehouseId);

        using (var first = Fixture.CreateScope())
            await first.ServiceProvider.GetRequiredService<ISender>()
                .Send(new UpdateCartStatusCommand(code, CartStatus.Confirmed));

        using var second = Fixture.CreateScope();
        await Assert.ThrowsAsync<BusinessRuleException>(() => second.ServiceProvider
            .GetRequiredService<ISender>()
            .Send(new UpdateCartStatusCommand(code, CartStatus.Confirmed)));
    }
}
