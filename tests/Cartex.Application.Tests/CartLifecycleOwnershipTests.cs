using Cartex.Application.Common.Messaging;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Ordering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class CartLifecycleOwnershipTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Claim_is_owned_and_cancelled_cart_requeues_as_new_immutable_cart()
    {
        long branchId;
        long warehouseId;
        long businessId;
        long adminId;
        long sellerId;
        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            sellerId = await db.Users.Where(x => x.Username == "seller").Select(x => x.Id).FirstAsync();
            variantId = await db.ProductVariants.Select(x => x.Id).FirstAsync();
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);

        string code;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            code = await sender.Send(new SubmitCartCommand(warehouseId, null, [new SubmitCartItemDto(variantId, 1)])
            {
                IdempotencyKey = "owned-cart-submit"
            });
            await sender.Send(new UpdateCartStatusCommand(code, CartStatus.Confirmed));
        }
        using (var scope = Fixture.CreateScope())
        {
            var cart = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Carts
                .SingleAsync(x => x.AggregateCode == code);
            Assert.Equal(adminId, cart.ClaimedByUserId);
            Assert.NotNull(cart.ClaimedAt);
            Assert.Equal(2, cart.Version);
        }

        Fixture.CurrentUser.AsCashier(sellerId, businessId, branchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Checkout);
        using (var scope = Fixture.CreateScope())
        {
            await Assert.ThrowsAsync<ConflictException>(() => scope.ServiceProvider
                .GetRequiredService<ISender>().Send(new CheckoutCartCommand(code, 0, 0, 0)));
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new UpdateCartStatusCommand(code, CartStatus.Cancelled, "Mijoz fikridan qaytdi"));
        }

        RequeueCartResult requeued;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new RequeueCartCommand(code, "Qayta tasdiqlandi", "owned-cart-requeue");
            requeued = await sender.Send(command);
            Assert.Equal(requeued, await sender.Send(command));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await db2.Carts.SingleAsync(x => x.AggregateCode == code);
        var clone = await db2.Carts.Include(x => x.Items)
            .SingleAsync(x => x.AggregateCode == requeued.AggregateCode);
        Assert.Equal(CartStatus.Cancelled, original.Status);
        Assert.Equal("Mijoz fikridan qaytdi", original.CancellationReason);
        Assert.Equal(CartStatus.Open, clone.Status);
        Assert.Equal(original.Id, clone.RequeuedFromCartId);
        Assert.Equal("Qayta tasdiqlandi", clone.Note);
        Assert.Single(clone.Items);
        Assert.Equal(1, await db2.Carts.CountAsync(x => x.RequeuedFromCartId == original.Id));
    }
}
