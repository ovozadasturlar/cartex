using Cartex.Application.Common.Messaging;
using Cartex.Application.Stocks.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class SearchTransliterationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task WP10_Latin_product_is_found_with_a_Cyrillic_query()
    {
        var context = await CreateProductsAsync(("Gisht", "WP10-GISHT"));

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var page = await sender.Send(new GetStockOnHandQuery(context.WarehouseId, Search: "Ғишт", PageSize: 50));

        Assert.Contains(page.Items, item => item.ProductName == "Gisht");
    }

    [Fact]
    public async Task WP10_Exact_q_match_is_ranked_before_the_fuzzy_k_match()
    {
        var context = await CreateProductsAsync(("Qora bo'yoq", "WP10-QORA"), ("Kora bo'yoq", "WP10-KORA"));

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var page = await sender.Send(new GetStockOnHandQuery(context.WarehouseId, Search: "қора", PageSize: 50));

        Assert.Equal(["Qora bo'yoq", "Kora bo'yoq"], page.Items.Select(item => item.ProductName).ToArray());
    }

    [Fact]
    public async Task WP10_Barcode_search_is_unchanged()
    {
        var context = await CreateProductsAsync(("Sinov mahsuloti", "WP10-BARCODE"));

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var page = await sender.Send(new GetStockOnHandQuery(context.WarehouseId, Search: "barcode:WP10-BARCODE", PageSize: 50));

        var item = Assert.Single(page.Items);
        Assert.Equal("Sinov mahsuloti", item.ProductName);
    }

    private async Task<SearchContext> CreateProductsAsync(params (string Name, string Barcode)[] products)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = await db.Branches.FirstAsync();
        var warehouse = await db.Warehouses.FirstAsync(x => x.BranchId == branch.Id);
        var unitId = await db.Units.Select(x => x.Id).FirstAsync();

        foreach (var (name, barcode) in products)
        {
            var variant = new ProductVariant
            {
                Product = new Product { Name = name, UnitId = unitId },
                IsDefault = true,
                Barcodes = [new Barcode { Code = barcode }]
            };
            db.ProductVariants.Add(variant);
            db.Stocks.Add(new Stock
            {
                BranchId = branch.Id,
                WarehouseId = warehouse.Id,
                Variant = variant,
                Quantity = 10m,
                PurchasePrice = 1m
            });
        }

        await db.SaveChangesAsync();
        return new SearchContext(warehouse.Id);
    }

    private sealed record SearchContext(long WarehouseId);
}
