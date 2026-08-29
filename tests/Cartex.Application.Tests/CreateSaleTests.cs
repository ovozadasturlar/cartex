using Cartex.Application.Sales.Commands;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class CreateSaleTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long branch1, long warehouse1, long businessId, long adminId, long variantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        return (branch1, warehouse1, businessId, adminId, variantId);
    }

    [Fact]
    public async Task Sale_decrements_stock_posts_ledger_and_writes_outbox()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();

        decimal stockBefore, cashBefore;
        int outboxBefore;
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockBefore = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            cashBefore = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            outboxBefore = await db.NotificationOutbox.CountAsync();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouse1, null, 770000, 0, 0, [new CreateSaleItemDto(variantId, 2)]));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stockAfter = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            var cashAfter = (await db.Accounts.FirstAsync(a => a.BranchId == branch1 && a.Type == AccountType.Cash)).Balance;
            var outboxAfter = await db.NotificationOutbox.CountAsync();

            Assert.Equal(stockBefore - 2, stockAfter);
            Assert.Equal(cashBefore + 770000, cashAfter);
            Assert.True(outboxAfter > outboxBefore);
        }
    }

    [Fact]
    public async Task Higher_entered_price_updates_the_catalog_price()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        decimal catalogPrice;
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            catalogPrice = await db.ProductPrices.Where(p => p.VariantId == variantId && p.WarehouseId == null)
                .Select(p => p.SellingPrice)
                .SingleAsync();
            var enteredPrice = catalogPrice + 15_000m;

            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, enteredPrice, 0, 0,
                [new CreateSaleItemDto(variantId, 1, enteredPrice)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var updatedPrice = await db.ProductPrices.Where(p => p.VariantId == variantId && p.WarehouseId == null)
                .Select(p => p.SellingPrice)
                .SingleAsync();
            var sale = await db.Sales.Include(s => s.Items).SingleAsync(s => s.Id == saleId);

            Assert.Equal(catalogPrice + 15_000m, updatedPrice);
            Assert.Equal(catalogPrice + 15_000m, sale.TotalAmount);
            Assert.Equal(0m, sale.DiscountAmount);
            Assert.Equal(catalogPrice + 15_000m, sale.Items.Single().UnitPrice);
        }
    }

    [Fact]
    public async Task Entered_price_creates_a_missing_catalog_price()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        const decimal enteredPrice = 425_000m;
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var prices = await db.ProductPrices.Where(p => p.VariantId == variantId).ToListAsync();
            db.ProductPrices.RemoveRange(prices);
            await db.SaveChangesAsync();

            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, enteredPrice, 0, 0,
                [new CreateSaleItemDto(variantId, 1, enteredPrice)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.ProductPrices.SingleAsync(p => p.VariantId == variantId && p.WarehouseId == null);
            var sale = await db.Sales.Include(s => s.Items).SingleAsync(s => s.Id == saleId);

            Assert.Equal(enteredPrice, price.SellingPrice);
            Assert.Equal(enteredPrice, sale.TotalAmount);
            Assert.Equal(enteredPrice, sale.Items.Single().UnitPrice);
        }
    }

    [Fact]
    public async Task Lower_entered_price_is_recorded_as_a_receipt_discount()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        decimal catalogPrice;
        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            catalogPrice = await db.ProductPrices.Where(p => p.VariantId == variantId && p.WarehouseId == null)
                .Select(p => p.SellingPrice)
                .SingleAsync();
            var enteredPrice = catalogPrice - 15_000m;

            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, enteredPrice, 0, 0,
                [new CreateSaleItemDto(variantId, 1, enteredPrice)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unchangedPrice = await db.ProductPrices.Where(p => p.VariantId == variantId && p.WarehouseId == null)
                .Select(p => p.SellingPrice)
                .SingleAsync();
            var sale = await db.Sales.Include(s => s.Items).SingleAsync(s => s.Id == saleId);

            Assert.Equal(catalogPrice, unchangedPrice);
            Assert.Equal(catalogPrice - 15_000m, sale.TotalAmount);
            Assert.Equal(15_000m, sale.DiscountAmount);
            Assert.Equal(catalogPrice, sale.Items.Single().UnitPrice);
        }
    }

    [Fact]
    public async Task Sale_with_insufficient_stock_throws_and_keeps_stock()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);
        decimal before;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            before = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateSaleCommand(warehouse1, null, 1_000_000_000m, 0, 0, [new CreateSaleItemDto(variantId, before + 1000)])));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var after = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public async Task Sale_with_insufficient_stock_uses_a_deficit_batch_and_activates_catalog()
    {
        var (branch1, warehouse1, businessId, adminId, variantId) = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);
        await TestShift.OpenAsync(Fixture);

        long saleId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var onHand = await db.Stocks.Where(s => s.VariantId == variantId && s.WarehouseId == warehouse1).SumAsync(s => s.Quantity);
            await settings.SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowInsufficientStockSales = true });

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouse1, null, (onHand + 2) * 385000m, 0, 0,
                [new CreateSaleItemDto(variantId, onHand + 2)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var deficit = await db.Stocks.SingleAsync(s => s.WarehouseId == warehouse1 && s.VariantId == variantId && s.IsDeficit);
            var item = await db.SaleItems.Include(x => x.Stock).SingleAsync(x => x.SaleId == saleId && x.Stock.IsDeficit);
            var catalog = await db.BranchCatalogEntries.SingleAsync(x => x.BranchId == branch1 && x.VariantId == variantId);

            Assert.Equal(-2m, deficit.Quantity);
            Assert.Equal(deficit.Id, item.StockId);
            Assert.True(item.Stock.IsDeficit);
            Assert.NotNull(catalog.FirstActivityAt);
        }
    }
}
