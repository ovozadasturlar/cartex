using Cartex.Application.Products.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Queries;
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
public class UpdateSupplyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    private async Task<(long warehouseId, long variantId)> SetupAsync()
    {
        long branchId, warehouseId, businessId, adminId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
            warehouseId = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
            businessId = (await db.Businesses.FirstAsync()).Id;
            adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);

        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unitId = (await db.Units.FirstAsync(u => u.ShortName == "dona")).Id;
            var productId = await sender.Send(new CreateProductCommand("Tahrir mahsuloti", null, unitId, 0, null));
            variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        }

        return (warehouseId, variantId);
    }

    private async Task<long> CreateSupplierAsync(string name = "Test Ta'minotchi")
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplierCommand(name, null));
    }

    private async Task<long> CreateSupplyAsync(long supplierId, long warehouseId, long variantId, decimal? sellingPrice = null)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CreateSupplyCommand(supplierId, warehouseId, Today,
            [new CreateSupplyItemDto(variantId, 5, 8000m, null, SellingPrice: sellingPrice)]));
    }

    private async Task PayAsync(long supplierId, long supplyId, decimal amount)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new PaySupplierDebtCommand(supplierId, amount, AccountType.Transfer, SupplyId: supplyId));
    }

    private async Task UpdateAsync(long supplyId, long supplierId, long warehouseId, long variantId, decimal quantity, decimal price)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new UpdateSupplyCommand(supplyId, supplierId, warehouseId, Today,
            [new CreateSupplyItemDto(variantId, quantity, price, null)]));
    }

    private async Task<decimal> PayableAsync(long supplierId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.Accounts.Where(a => a.SupplierId == supplierId && a.Type == AccountType.Debt)
            .Select(a => (decimal?)a.Balance).FirstOrDefaultAsync() ?? 0;
        return -balance;
    }

    private async Task<decimal> SupplyStockAsync(long supplyId, long variantId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stocks.Where(s => s.SupplyId == supplyId && s.VariantId == variantId).SumAsync(s => s.Quantity);
    }

    private async Task<SupplyDetailDto> SupplyAsync(long supplyId)
    {
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return (await sender.Send(new GetSupplyByIdQuery(supplyId)))!;
    }

    [Fact]
    public async Task Update_RewritesStockAndDebt()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supplyId = await CreateSupplyAsync(supplierId, warehouseId, variantId);

        Assert.Equal(40_000m, await PayableAsync(supplierId));
        Assert.Equal(5m, await SupplyStockAsync(supplyId, variantId));

        await UpdateAsync(supplyId, supplierId, warehouseId, variantId, 3, 10_000m);

        Assert.Equal(30_000m, await PayableAsync(supplierId));
        Assert.Equal(3m, await SupplyStockAsync(supplyId, variantId));

        var supply = await SupplyAsync(supplyId);
        Assert.Equal(30_000m, supply.TotalAmount);
        var item = Assert.Single(supply.Items);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(10_000m, item.PurchasePrice);
    }

    [Fact]
    public async Task Update_KeepsAttachedPayment()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supplyId = await CreateSupplyAsync(supplierId, warehouseId, variantId);

        await PayAsync(supplierId, supplyId, 40_000m);
        Assert.Equal(0m, await PayableAsync(supplierId));

        await UpdateAsync(supplyId, supplierId, warehouseId, variantId, 5, 10_000m);

        var supply = await SupplyAsync(supplyId);
        Assert.Equal(50_000m, supply.TotalAmount);
        Assert.Equal(40_000m, supply.PaidTransfer);
        Assert.Equal(10_000m, await PayableAsync(supplierId));
    }

    [Fact]
    public async Task Update_BelowPaid_Throws()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplierId = await CreateSupplierAsync();
        var supplyId = await CreateSupplyAsync(supplierId, warehouseId, variantId);

        await PayAsync(supplierId, supplyId, 40_000m);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UpdateAsync(supplyId, supplierId, warehouseId, variantId, 3, 10_000m));

        var supply = await SupplyAsync(supplyId);
        Assert.Equal(40_000m, supply.TotalAmount);
        Assert.Equal(40_000m, supply.PaidTransfer);
        Assert.Equal(5m, await SupplyStockAsync(supplyId, variantId));
        Assert.Equal(0m, await PayableAsync(supplierId));
    }

    [Fact]
    public async Task Update_SupplierChangeWithPayment_Throws()
    {
        var (warehouseId, variantId) = await SetupAsync();
        var supplier1 = await CreateSupplierAsync("Ta'minotchi 1");
        var supplier2 = await CreateSupplierAsync("Ta'minotchi 2");
        var supplyId = await CreateSupplyAsync(supplier1, warehouseId, variantId);

        await PayAsync(supplier1, supplyId, 40_000m);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UpdateAsync(supplyId, supplier2, warehouseId, variantId, 5, 8000m));

        Assert.Equal(supplier1, (await SupplyAsync(supplyId)).SupplierId);
        Assert.Equal(0m, await PayableAsync(supplier2));
    }

    [Fact]
    public async Task Update_ConsumedStock_Throws()
    {
        var (warehouseId, variantId) = await SetupAsync();
        await TestShift.OpenAsync(Fixture);
        var supplierId = await CreateSupplierAsync();
        var supplyId = await CreateSupplyAsync(supplierId, warehouseId, variantId, sellingPrice: 12_000m);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateSaleCommand(warehouseId, null, 12_000m, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UpdateAsync(supplyId, supplierId, warehouseId, variantId, 3, 10_000m));

        Assert.Equal(40_000m, (await SupplyAsync(supplyId)).TotalAmount);
        Assert.Equal(40_000m, await PayableAsync(supplierId));
    }
}
