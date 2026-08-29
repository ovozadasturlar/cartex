using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md QAYT-08. Voiding a sale already puts the goods back and
/// takes the money out; a return on top of that would do both a second time.
[Collection("database")]
public class VoidedSaleReturnTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private sealed record Setup(long Warehouse, long Business, long Admin, long Branch, long VariantId);

    private async Task<Setup> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var business = (await db.Businesses.FirstAsync()).Id;
        var admin = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(admin, business, branch);
        await TestShift.OpenAsync(Fixture);

        var stocked = db.Stocks.Where(s => s.WarehouseId == warehouse && s.Quantity >= 3).Select(s => s.VariantId);
        var variantId = await db.ProductPrices
            .Where(p => p.WarehouseId == null && stocked.Contains(p.VariantId))
            .Select(p => p.VariantId)
            .OrderBy(x => x)
            .FirstAsync();

        return new Setup(warehouse, business, admin, branch, variantId);
    }

    private async Task<long> SellAsync(Setup s)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new CreateSaleCommand(s.Warehouse, null, 1_000_000m, 0, 0,
            [new CreateSaleItemDto(s.VariantId, 1)])
        {
            ApplyAutoDiscount = false
        })).SaleId;
    }

    [Fact]
    public async Task QAYT_08_A_voided_sale_cannot_be_returned()
    {
        var s = await SetupAsync();
        var saleId = await SellAsync(s);

        using (var scope = Fixture.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new VoidSaleCommand(saleId, "Xato kiritildi"));

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(SaleStatus.Voided, (await db.Sales.AsNoTracking().SingleAsync(x => x.Id == saleId)).Status);
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId);
            var command = await TestReturns.ForItemAsync(db, item.Id, 1m);

            var error = await Assert.ThrowsAnyAsync<Exception>(() => sender.Send(command));

            Assert.True(error is BusinessRuleException { Code: "sale_not_returnable" },
                $"expected sale_not_returnable, got {error.GetType().Name}: {error.Message}");
        }
    }

    [Fact]
    public async Task QAYT_08_A_completed_sale_is_still_returnable()
    {
        var s = await SetupAsync();
        var saleId = await SellAsync(s);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var item = await db.SaleItems.AsNoTracking().FirstAsync(x => x.SaleId == saleId);

        var result = await sender.Send(await TestReturns.ForItemAsync(db, item.Id, 1m));

        Assert.True(result.RefundAmount > 0);
    }
}
