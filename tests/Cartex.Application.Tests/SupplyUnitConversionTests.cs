using Cartex.Application.Common.Messaging;
using Cartex.Application.ProductPacks.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SupplyUnitConversionTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long WarehouseId, long SupplierId, long KgUnitId, long TonUnitId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var kgUnitId = (await db.Units.FirstAsync(u => u.ShortName == "kg")).Id;
        var tonUnitId = (await db.Units.FirstAsync(u => u.ShortName == "t")).Id;

        var supplier = new Supplier { Name = "Konversiya ta'minotchisi" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        return (warehouseId, supplier.Id, kgUnitId, tonUnitId);
    }

    private async Task<long> CreateKgProductAsync(long kgUnitId, string name)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var productId = await sender.Send(new CreateProductCommand(name, null, kgUnitId, 0, null));
        return (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
    }

    [Fact]
    public async Task Ton_and_kilogram_entries_produce_the_same_stock_and_money()
    {
        var (warehouseId, supplierId, kgUnitId, tonUnitId) = await SetupAsync();
        var kgVariant = await CreateKgProductAsync(kgUnitId, "Shakar (kg kirim)");
        var tonVariant = await CreateKgProductAsync(kgUnitId, "Shakar (tonna kirim)");

        long kgSupplyId, tonSupplyId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            kgSupplyId = await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(kgVariant, 500m, 12_000m, null)]));
            tonSupplyId = await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(tonVariant, 0.5m, 12_000_000m, null, UnitId: tonUnitId)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var kgStock = await db.Stocks.SingleAsync(s => s.VariantId == kgVariant);
            var tonStock = await db.Stocks.SingleAsync(s => s.VariantId == tonVariant);

            Assert.Equal(500m, tonStock.Quantity);
            Assert.Equal(kgStock.Quantity, tonStock.Quantity);
            Assert.Equal(12_000m, tonStock.PurchasePrice);
            Assert.Equal(kgStock.PurchasePrice, tonStock.PurchasePrice);

            var kgTotal = await db.Supplies.Where(s => s.Id == kgSupplyId).Select(s => s.TotalAmount).SingleAsync();
            var tonTotal = await db.Supplies.Where(s => s.Id == tonSupplyId).Select(s => s.TotalAmount).SingleAsync();
            Assert.Equal(6_000_000m, tonTotal);
            Assert.Equal(kgTotal, tonTotal);
        }
    }

    [Fact]
    public async Task Selling_price_is_never_converted_to_the_entry_unit()
    {
        var (warehouseId, supplierId, kgUnitId, tonUnitId) = await SetupAsync();
        var variantId = await CreateKgProductAsync(kgUnitId, "Guruch (tonna kirim)");

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 1m, 12_000_000m, null, UnitId: tonUnitId, SellingPrice: 15_000m)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.ProductPrices.SingleAsync(p => p.VariantId == variantId);
            Assert.Equal(15_000m, price.SellingPrice);
        }
    }

    [Fact]
    public async Task Pack_entry_matches_a_plain_weight_entry()
    {
        var (warehouseId, supplierId, kgUnitId, _) = await SetupAsync();
        var variantId = await CreateKgProductAsync(kgUnitId, "Shakar (qop kirim)");

        long packId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = (await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).SingleAsync());

            packId = await sender.Send(new CreateProductPackCommand(productId, "Qop", 50m, PackKind.Purchase, true));

            await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10m, 600_000m, null, PackId: packId)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stock = await db.Stocks.SingleAsync(s => s.VariantId == variantId);
            Assert.Equal(500m, stock.Quantity);
            Assert.Equal(12_000m, stock.PurchasePrice);

            var line = await db.SupplyItems.SingleAsync(i => i.VariantId == variantId);
            Assert.Equal(10m, line.EntryQuantity);
            Assert.Equal(600_000m, line.EntryPrice);
            Assert.Equal(50m, line.PackSize);
            Assert.Equal(packId, line.PackId);
        }
    }

    [Fact]
    public async Task Pack_entry_with_price_per_stocking_unit()
    {
        var (warehouseId, supplierId, kgUnitId, _) = await SetupAsync();
        var variantId = await CreateKgProductAsync(kgUnitId, "Sement (qop, kg narxi)");

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).SingleAsync();
            var packId = await sender.Send(new CreateProductPackCommand(productId, "Qop", 50m, PackKind.Purchase, false));

            await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 10m, 12_000m, null, PackId: packId, PriceBasis: SupplyPriceBasis.PerStockingUnit)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stock = await db.Stocks.SingleAsync(s => s.VariantId == variantId);
            Assert.Equal(500m, stock.Quantity);
            Assert.Equal(12_000m, stock.PurchasePrice);

            var total = await db.Supplies.OrderByDescending(s => s.Id).Select(s => s.TotalAmount).FirstAsync();
            Assert.Equal(6_000_000m, total);
        }
    }

    [Fact]
    public async Task Sale_only_pack_is_rejected_in_a_supply()
    {
        var (warehouseId, supplierId, kgUnitId, _) = await SetupAsync();
        var variantId = await CreateKgProductAsync(kgUnitId, "Shakar (sotuv qadog'i)");

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var productId = await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.ProductId).SingleAsync();
        var packId = await sender.Send(new CreateProductPackCommand(productId, "1 kg paket", 1m, PackKind.Sale, false));

        await Assert.ThrowsAsync<Cartex.Domain.Common.Exceptions.BusinessRuleException>(() =>
            sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5m, 5_000m, null, PackId: packId)])));
    }

    [Fact]
    public async Task Entry_unit_of_another_dimension_is_rejected()
    {
        var (warehouseId, supplierId, kgUnitId, _) = await SetupAsync();
        var variantId = await CreateKgProductAsync(kgUnitId, "Un (o'lchov xatosi)");

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pieceUnitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<Cartex.Domain.Common.Exceptions.BusinessRuleException>(() =>
            sender.Send(new CreateSupplyCommand(supplierId, warehouseId, DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(variantId, 5m, 1_000m, null, UnitId: pieceUnitId)])));
    }
}
