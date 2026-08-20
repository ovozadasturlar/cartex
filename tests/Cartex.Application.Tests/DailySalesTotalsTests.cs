using Cartex.Application.Common.Messaging;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class DailySalesTotalsTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branchId, long warehouseId, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branchId, warehouseId, businessId, adminId, variantId);
    }

    [Fact]
    public async Task Daily_totals_group_sales_by_local_day()
    {
        var (branchId, warehouseId, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        decimal firstTotal;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouseId, null, 100_000_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]));
            await sender.Send(new CreateSaleCommand(warehouseId, null, 100_000_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 2)]));
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            firstTotal = await db.Sales.SumAsync(s => s.TotalAmount);
        }

        List<Cartex.Shared.Models.Sales.DailySalesPointDto> points;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            points = await sender.Send(new GetDailySalesQuery
            {
                FromDate = DateTime.UtcNow.AddDays(-6),
                ToDate = DateTime.UtcNow.AddDays(1),
                TzOffsetMinutes = 300
            });
        }

        var day = Assert.Single(points);
        Assert.Equal(2, day.Count);
        Assert.Equal(firstTotal, day.TotalAmount);
    }

    [Fact]
    public async Task Daily_totals_return_empty_when_range_has_no_sales()
    {
        var (branchId, _, businessId, adminId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var points = await sender.Send(new GetDailySalesQuery
        {
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(-20),
            TzOffsetMinutes = 300
        });

        Assert.Empty(points);
    }
}
