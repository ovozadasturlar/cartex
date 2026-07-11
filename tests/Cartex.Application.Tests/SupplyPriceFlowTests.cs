using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SupplyPriceFlowTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long unitId, long supplierId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var unitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;

        var supplier = new Supplier { Name = "Test Supplier" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return (branch1, warehouse1, businessId, adminId, unitId, supplier.Id);
    }

    [Fact]
    public async Task Supply_sets_selling_price_and_sale_uses_it()
    {
        var (branch1, warehouse1, businessId, adminId, unitId, supplierId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        const decimal sellingPrice = 12000m;
        long variantId;

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var productId = await sender.Send(new CreateProductCommand("Narxsiz mahsulot", null, unitId, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;

            await sender.Send(new CreateSupplyCommand(supplierId, warehouse1, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10, 8000m, null, SellingPrice: sellingPrice)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.ProductPrices.SingleAsync(p => p.VariantId == variantId);
            Assert.Null(price.WarehouseId);
            Assert.Equal(sellingPrice, price.SellingPrice);
        }

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, sellingPrice, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unitPrice = await db.SaleItems.Where(i => i.SaleId == saleId).Select(i => i.UnitPrice).SingleAsync();
            Assert.Equal(sellingPrice, unitPrice);
        }
    }
}
