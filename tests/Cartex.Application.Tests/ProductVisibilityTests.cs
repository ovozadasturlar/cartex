using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Products.Commands;
using Cartex.Application.Products.Queries;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Stocks.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ProductVisibilityTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long productId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, productId, variantId);
    }

    private async Task SetEnabledAsync(long productId, bool isEnabled)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsEnabled, isEnabled));
    }

    private async Task SetShowOutOfStockAsync(bool showOutOfStock)
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { ShowOutOfStock = showOutOfStock });
    }

    private async Task ZeroOutStockAsync(long variantId, long warehouseId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouseId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, 0m));
    }

    private async Task<long> CreateUnstockedVariantAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unitId = await db.Units.Select(x => x.Id).FirstAsync();
        var variant = new ProductVariant
        {
            Product = new Product { Name = $"Omborsiz mahsulot {Guid.NewGuid():N}", UnitId = unitId },
            IsDefault = true
        };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return variant.Id;
    }

    private async Task<StockOnHandPageDto> OnHandAsync(long warehouseId, bool forSale)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetStockOnHandQuery(warehouseId, PageSize: 500, ForSale: forSale));
    }

    private async Task<string> BarcodeOfAsync(long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Barcodes.FirstAsync(b => b.VariantId == variantId)).Code;
    }

    [Fact]
    public async Task Disabled_product_is_hidden_for_sale_but_visible_for_warehouse()
    {
        var (branch1, warehouse1, businessId, adminId, productId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var beforeForSale = await OnHandAsync(warehouse1, forSale: true);
        Assert.Contains(beforeForSale.Items, i => i.VariantId == variantId);

        await SetEnabledAsync(productId, false);

        var forSale = await OnHandAsync(warehouse1, forSale: true);
        Assert.DoesNotContain(forSale.Items, i => i.VariantId == variantId);
        Assert.Equal(forSale.Items.Count, forSale.TotalCount);
        Assert.Equal(beforeForSale.TotalCount - 1, forSale.TotalCount);

        var management = await OnHandAsync(warehouse1, forSale: false);
        Assert.Contains(management.Items, i => i.VariantId == variantId);
    }

    [Fact]
    public async Task Out_of_stock_variant_is_hidden_when_policy_forbids_it()
    {
        var (branch1, warehouse1, businessId, adminId, _, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await SetShowOutOfStockAsync(false);
        var withStock = await OnHandAsync(warehouse1, forSale: true);
        Assert.Contains(withStock.Items, i => i.VariantId == variantId);

        await ZeroOutStockAsync(variantId, warehouse1);

        var hidden = await OnHandAsync(warehouse1, forSale: true);
        Assert.DoesNotContain(hidden.Items, i => i.VariantId == variantId);
        Assert.Equal(hidden.Items.Count, hidden.TotalCount);
        Assert.Equal(withStock.TotalCount - 1, hidden.TotalCount);
    }

    [Fact]
    public async Task Out_of_stock_variant_is_shown_when_policy_allows_it()
    {
        var (branch1, warehouse1, businessId, adminId, _, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        await ZeroOutStockAsync(variantId, warehouse1);
        await SetShowOutOfStockAsync(true);

        var shown = await OnHandAsync(warehouse1, forSale: true);
        var item = Assert.Single(shown.Items, i => i.VariantId == variantId);
        Assert.Equal(0m, item.Quantity);
        Assert.Equal(shown.Items.Count, shown.TotalCount);
    }

    [Fact]
    public async Task Unstocked_variant_is_shown_when_policy_allows_it()
    {
        var (branch1, warehouse1, businessId, adminId, _, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        var variantId = await CreateUnstockedVariantAsync();

        await SetShowOutOfStockAsync(true);

        var shown = await OnHandAsync(warehouse1, forSale: true);
        var item = Assert.Single(shown.Items, i => i.VariantId == variantId);
        Assert.Equal(0m, item.Quantity);
        Assert.Empty(item.Barcodes!);
    }

    [Fact]
    public async Task Barcode_lookup_hides_disabled_product_only_for_sale()
    {
        var (branch1, warehouse1, businessId, adminId, productId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var code = await BarcodeOfAsync(variantId);
        await SetEnabledAsync(productId, false);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        Assert.Null(await sender.Send(new GetProductByBarcodeQuery(code, warehouse1, ForSale: true)));

        var lookup = await sender.Send(new GetProductByBarcodeQuery(code, warehouse1, ForSale: false));
        Assert.NotNull(lookup);
        Assert.Equal(variantId, lookup.VariantId);
    }

    [Fact]
    public async Task Set_product_state_persists_the_flag()
    {
        var (branch1, _, businessId, adminId, productId, _) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SetProductStateCommand(productId, false));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Products.Where(p => p.Id == productId).Select(p => p.IsEnabled).FirstAsync());
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SetProductStateCommand(productId, true));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.Products.Where(p => p.Id == productId).Select(p => p.IsEnabled).FirstAsync());
        }
    }

    [Fact]
    public async Task Sale_of_disabled_product_is_rejected()
    {
        var (branch1, warehouse1, businessId, adminId, productId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        await SetEnabledAsync(productId, false);

        decimal stockBefore;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockBefore = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, null, 385000, 0, 0, [new CreateSaleItemDto(variantId, 1)])));
            Assert.Contains("Smesitel oshxona Zegor", ex.Message);
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stockAfter = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            Assert.Equal(stockBefore, stockAfter);
        }
    }
}
