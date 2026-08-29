using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ProductLifecycleTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Create_without_barcode_generates_default_barcode()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var unitId = await db.Units.Where(x => x.ShortName == "dona").Select(x => x.Id).FirstAsync();

        var productId = await sender.Send(new CreateProductCommand("Avto barkod", null, unitId, 0, null));
        var variantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
        var barcode = await db.Barcodes.Where(x => x.VariantId == variantId).SingleAsync();

        Assert.Equal($"CTX-{variantId:D6}", barcode.Code);
        Assert.Equal(1, barcode.PackQty);
    }

    [Fact]
    public async Task Delete_unused_product_soft_deletes_catalog_entities()
    {
        long productId;
        long variantId;

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var unitId = await db.Units.Where(x => x.ShortName == "dona").Select(x => x.Id).FirstAsync();
            productId = await sender.Send(new CreateProductCommand("O'chiriladigan mahsulot", null, unitId, 0, null));
            variantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
            await sender.Send(new DeleteProductCommand(productId));
        }

        using var verifyScope = Fixture.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await verifyDb.Products.FirstOrDefaultAsync(x => x.Id == productId));
        Assert.True(await verifyDb.Products.IgnoreQueryFilters().Where(x => x.Id == productId).Select(x => x.IsDeleted).SingleAsync());
        Assert.True(await verifyDb.ProductVariants.IgnoreQueryFilters().Where(x => x.Id == variantId).Select(x => x.IsDeleted).SingleAsync());
        Assert.True(await verifyDb.Barcodes.IgnoreQueryFilters().Where(x => x.VariantId == variantId).Select(x => x.IsDeleted).SingleAsync());
    }
}
