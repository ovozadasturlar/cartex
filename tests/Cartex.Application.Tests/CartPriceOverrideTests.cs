using Cartex.Application.Common.Messaging;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Cartex.Shared.Models.Ordering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class CartPriceOverrideTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Seed(long BranchId, long WarehouseId, long BusinessId, long UserId, long VariantId, decimal CatalogPrice);

    private async Task<Seed> SeedAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
        var warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
        var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
        var userId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
        var baseCurrency = await db.Businesses.Select(x => x.Currency).FirstAsync();
        var candidate = await (
            from stock in db.Stocks
            join price in db.ProductPrices on stock.VariantId equals price.VariantId
            where stock.WarehouseId == warehouseId && stock.Quantity > 0
                  && price.SellingPrice > 1000
                  && (price.Currency == baseCurrency || price.Currency == "")
            select new { stock.VariantId, price.SellingPrice }).FirstAsync();
        return new Seed(branchId, warehouseId, businessId, userId, candidate.VariantId, candidate.SellingPrice);
    }

    [Fact]
    public async Task Submit_with_price_requires_permission()
    {
        var seed = await SeedAsync();
        Fixture.CurrentUser.AsCashier(seed.UserId, seed.BusinessId, seed.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Create);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(new SubmitCartCommand(
            seed.WarehouseId, null,
            [new SubmitCartItemDto(seed.VariantId, 1, seed.CatalogPrice - 500)])));
    }

    [Fact]
    public async Task Queued_override_survives_checkout_by_user_without_permission()
    {
        var seed = await SeedAsync();
        var overridePrice = seed.CatalogPrice - 500;
        Fixture.CurrentUser.AsAdmin(seed.UserId, seed.BusinessId, seed.BranchId);
        await TestShift.OpenAsync(Fixture);

        Fixture.CurrentUser.AsCashier(seed.UserId, seed.BusinessId, seed.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Create);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Checkout);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.PriceOverride);

        string code;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            code = await sender.Send(new SubmitCartCommand(
                seed.WarehouseId, null,
                [new SubmitCartItemDto(seed.VariantId, 1, overridePrice)]));
        }

        Fixture.CurrentUser.Granted.Remove(AppPermissions.Sales.PriceOverride);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = await sender.Send(new CheckoutCartCommand(code, overridePrice, 0, 0));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sale = await db.Sales.Include(x => x.Items).FirstAsync(x => x.Id == saleId);
            Assert.Equal(seed.CatalogPrice - overridePrice, sale.DiscountAmount);
            Assert.Equal(seed.CatalogPrice, sale.Items.Single().UnitPrice);
        }
    }

    [Fact]
    public async Task Checkout_with_new_override_requires_permission()
    {
        var seed = await SeedAsync();
        Fixture.CurrentUser.AsAdmin(seed.UserId, seed.BusinessId, seed.BranchId);
        await TestShift.OpenAsync(Fixture);

        Fixture.CurrentUser.AsCashier(seed.UserId, seed.BusinessId, seed.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Create);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Sales.Checkout);

        string code;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            code = await sender.Send(new SubmitCartCommand(
                seed.WarehouseId, null,
                [new SubmitCartItemDto(seed.VariantId, 1)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() => sender.Send(new CheckoutCartCommand(
                code, seed.CatalogPrice, 0, 0,
                Items: [new CheckoutCartItemDto(seed.VariantId, 1, seed.CatalogPrice - 500)])));
            Assert.Contains("narxni o'zgartirish", forbidden.Message);
        }
    }
}
