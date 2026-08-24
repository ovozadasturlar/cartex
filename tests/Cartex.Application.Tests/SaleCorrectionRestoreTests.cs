using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Stocks.Queries;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class SaleCorrectionRestoreTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private const decimal CatalogPrice = 100_000m;

    private sealed record Context(
        long WarehouseId,
        long OtherWarehouseId,
        long UnitId,
        long VariantId);

    private async Task<Context> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = await db.Branches.FirstAsync(x => x.Name == "Asosiy filial");
        var otherBranch = await db.Branches.FirstAsync(x => x.Id != branch.Id);
        var warehouse = await db.Warehouses.FirstAsync(x => x.BranchId == branch.Id);
        var otherWarehouse = await db.Warehouses.FirstAsync(x => x.BranchId == otherBranch.Id);
        var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
        var unitId = await db.Units.Where(x => x.ShortName == "dona").Select(x => x.Id).FirstAsync();
        var productId = await db.Products.Where(x => x.Name == "Smesitel oshxona Zegor").Select(x => x.Id).FirstAsync();
        var variantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).FirstAsync();
        await db.ProductPrices.Where(x => x.VariantId == variantId)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.SellingPrice, CatalogPrice));

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch.Id, otherBranch.Id);
        await TestShift.OpenAsync(Fixture);

        return new Context(
            warehouse.Id,
            otherWarehouse.Id,
            unitId,
            variantId);
    }

    private async Task<long> SellAsync(
        Context context,
        decimal quantity,
        decimal enteredUnitPrice,
        decimal manualDiscountAmount,
        decimal paidCash,
        long? customerId = null,
        DateOnly? debtDueDate = null)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(
            context.WarehouseId,
            customerId,
            paidCash,
            0,
            0,
            [new CreateSaleItemDto(context.VariantId, quantity, enteredUnitPrice, ExpectedUnitPrice: CatalogPrice)])
        {
            DiscountAmount = manualDiscountAmount,
            DebtDueDate = debtDueDate,
            ApplyAutoDiscount = false
        });
        return result.SaleId;
    }

    private async Task<Cartex.Shared.Models.Sales.SaleDetailDto> DetailAsync(long saleId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetSaleByIdQuery(saleId));
    }

    [Fact]
    public async Task TUZ_02_TUZ_03_Detail_preserves_entered_price_and_manual_discount_separately()
    {
        var context = await SetupAsync();
        var saleId = await SellAsync(context, 2, 90_000m, 5_000m, 175_000m);

        var detail = await DetailAsync(saleId);
        var item = Assert.Single(detail.Items);

        Assert.Equal(CatalogPrice, item.UnitPrice);
        Assert.Equal(90_000m, item.EnteredUnitPrice);
        Assert.Equal(5_000m, detail.ManualDiscountAmount);
        Assert.Equal(25_000m, detail.DiscountAmount);
    }

    [Fact]
    public async Task TUZ_02_Entered_price_equals_catalog_price_when_price_was_not_reduced()
    {
        var context = await SetupAsync();
        var saleId = await SellAsync(context, 1, CatalogPrice, 0, CatalogPrice);

        var item = Assert.Single((await DetailAsync(saleId)).Items);

        Assert.Equal(item.UnitPrice, item.EnteredUnitPrice);
        Assert.Equal(CatalogPrice, item.EnteredUnitPrice);
    }

    [Fact]
    public async Task TUZ_03_Manual_discount_is_zero_when_cashier_entered_no_header_discount()
    {
        var context = await SetupAsync();
        var saleId = await SellAsync(context, 1, 90_000m, 0, 90_000m);

        var detail = await DetailAsync(saleId);

        Assert.Equal(0m, detail.ManualDiscountAmount);
        Assert.Equal(10_000m, detail.DiscountAmount);
    }

    [Fact]
    public async Task TUZ_05_Detail_returns_the_sale_debt_due_date()
    {
        var context = await SetupAsync();
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISettingsService>()
                .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { AllowCustomerCredit = true });
        }

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            customerId = await sender.Send(new CreateCustomerCommand(
                "TUZ-05 mijoz",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null,
                0,
                CreditLimit: 1_000_000m));
        }

        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14));
        var saleId = await SellAsync(context, 1, CatalogPrice, 0, 0, customerId, dueDate);

        var detail = await DetailAsync(saleId);

        Assert.Equal(dueDate, detail.DebtDueDate);
    }

    [Fact]
    public async Task TUZ_06_By_variants_returns_current_warehouse_stock_and_excludes_other_branch_variant()
    {
        var context = await SetupAsync();
        long otherVariantId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var productId = await sender.Send(new CreateProductCommand(
                "TUZ-06 boshqa filial mahsuloti",
                null,
                context.UnitId,
                0,
                null));
            otherVariantId = await db.ProductVariants.Where(x => x.ProductId == productId).Select(x => x.Id).SingleAsync();
            await sender.Send(new CreateSupplyCommand(
                null,
                context.OtherWarehouseId,
                DateOnly.FromDateTime(DateTime.Today),
                [new CreateSupplyItemDto(otherVariantId, 7, 50_000m, null, SellingPrice: 70_000m)]));
        }

        decimal expectedQuantity;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            expectedQuantity = await db.Stocks
                .Where(x => x.WarehouseId == context.WarehouseId && x.VariantId == context.VariantId)
                .SumAsync(x => x.Quantity);
        }

        using var queryScope = Fixture.CreateScope();
        var querySender = queryScope.ServiceProvider.GetRequiredService<ISender>();
        var rows = await querySender.Send(new GetStockOnHandByVariantsQuery(
            context.WarehouseId,
            [context.VariantId, otherVariantId]));

        var row = Assert.Single(rows);
        Assert.Equal(context.VariantId, row.VariantId);
        Assert.Equal(expectedQuantity, row.Quantity);
        Assert.DoesNotContain(rows, x => x.VariantId == otherVariantId);
    }
}
