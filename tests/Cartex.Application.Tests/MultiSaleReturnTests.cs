using Cartex.Application.Common.Messaging;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class MultiSaleReturnTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, long WarehouseId, long BusinessId, long AdminId, long VariantId)> SetupAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = (await db.Branches.FirstAsync(x => x.Name == "Asosiy filial")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(x => x.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(x => x.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(x => x.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(x => x.ProductId == productId)).Id;
        return (branchId, warehouseId, businessId, adminId, variantId);
    }

    private async Task<long> CreateCustomerAsync(string name)
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerCommand(name, "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null, 0m, CreditLimit: 100_000_000m));
    }

    private async Task<long> CreateDebtSaleAsync(long warehouseId, long customerId, long variantId, decimal quantity)
    {
        using var scope = Fixture.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateSaleCommand(warehouseId, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, quantity)])
            {
                DebtDueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
            })).SaleId;
    }

    private async Task<decimal> DebtAsync(long customerId)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Accounts.Where(x => x.CustomerId == customerId && x.Type == AccountType.Debt)
            .SumAsync(x => x.Balance);
    }

    [Fact]
    public async Task One_document_returns_lines_from_two_sales_and_clears_both_debts()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateCustomerAsync("Ko'p savdo qaytaruvi");

        var firstSaleId = await CreateDebtSaleAsync(setup.WarehouseId, customerId, setup.VariantId, 2);
        var secondSaleId = await CreateDebtSaleAsync(setup.WarehouseId, customerId, setup.VariantId, 3);
        var debtBefore = await DebtAsync(customerId);
        Assert.True(debtBefore > 0);

        long documentId;
        decimal refundAmount;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var items = await db.SaleItems
                .Where(x => x.SaleId == firstSaleId || x.SaleId == secondSaleId)
                .ToListAsync();
            Assert.Equal(2, items.Count);

            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerReturnCommand(
                    setup.WarehouseId,
                    [.. items.Select(x => TestReturns.Line(x))],
                    customerId));
            documentId = result.Id;
            refundAmount = result.RefundAmount;
        }

        using var check = Fixture.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await verify.CustomerReturnDocuments
            .Include(x => x.Lines)
            .Include(x => x.Settlements)
            .FirstAsync(x => x.Id == documentId);

        Assert.Equal(2, document.Lines.Count);
        Assert.Equal([firstSaleId, secondSaleId], document.Lines.Select(x => x.SaleId!.Value).Order());
        Assert.All(document.Settlements, x => Assert.Equal(ReturnSettlementMethod.ReduceDebt, x.Method));
        Assert.Equal(refundAmount, document.Settlements.Sum(x => x.AmountBase));
        Assert.Equal(debtBefore - refundAmount, await DebtAsync(customerId));

        var statuses = await verify.Sales
            .Where(x => x.Id == firstSaleId || x.Id == secondSaleId)
            .Select(x => x.Status)
            .ToListAsync();
        Assert.All(statuses, x => Assert.Equal(SaleStatus.Returned, x));
    }

    [Fact]
    public async Task Free_line_uses_the_entered_price_and_still_reduces_debt()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateCustomerAsync("Erkin qator qaytaruvi");

        await CreateDebtSaleAsync(setup.WarehouseId, customerId, setup.VariantId, 1);
        var debtBefore = await DebtAsync(customerId);

        long documentId;
        using (var scope = Fixture.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerReturnCommand(
                    setup.WarehouseId,
                    [new CustomerReturnLineInput(setup.VariantId, 1m, null, 40_000m, "Ortiqcha qoldi",
                        ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)],
                    customerId));
            documentId = result.Id;
            Assert.Equal(40_000m, result.RefundAmount);
        }

        using var check = Fixture.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var line = await verify.CustomerReturnLines.SingleAsync(x => x.CustomerReturnDocumentId == documentId);
        Assert.Null(line.SaleId);
        Assert.Null(line.SaleItemId);
        Assert.NotNull(line.StockId);
        Assert.Equal(40_000m, line.UnitPrice);
        Assert.Equal(debtBefore - 40_000m, await DebtAsync(customerId));
    }

    [Fact]
    public async Task Return_without_customer_requires_an_explicit_settlement()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);

        var lines = new List<CustomerReturnLineInput>
        {
            new(setup.VariantId, 1m, null, 25_000m, null,
                ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)
        };

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                sender.Send(new CreateCustomerReturnCommand(setup.WarehouseId, lines)));
        }

        using (var scope = Fixture.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerReturnCommand(
                    setup.WarehouseId,
                    lines,
                    Settlements: [new CustomerReturnSettlementInput(ReturnSettlementMethod.NoCharge, "UZS", 25_000m)],
                    AutoSettle: false));
            Assert.Equal(25_000m, result.RefundAmount);
        }
    }

    [Fact]
    public async Task Returning_more_than_the_remaining_quantity_is_rejected()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateCustomerAsync("Ortiqcha qaytaruv");
        var saleId = await CreateDebtSaleAsync(setup.WarehouseId, customerId, setup.VariantId, 1);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.SaleItems.SingleAsync(x => x.SaleId == saleId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new CreateCustomerReturnCommand(
                setup.WarehouseId,
                [TestReturns.Line(item, item.Quantity + 1)],
                customerId)));
        Assert.Equal("return_quantity_exceeded", error.Code);
    }

    [Fact]
    public async Task Defective_line_goes_to_the_supplier_claim_bucket_instead_of_stock()
    {
        var setup = await SetupAsync();
        Fixture.CurrentUser.AsAdmin(setup.AdminId, setup.BusinessId, setup.BranchId);
        await TestShift.OpenAsync(Fixture);
        var customerId = await CreateCustomerAsync("Nuqsonli qaytaruv");
        var saleId = await CreateDebtSaleAsync(setup.WarehouseId, customerId, setup.VariantId, 1);

        decimal stockBefore;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            stockBefore = await db.Stocks
                .Where(x => x.VariantId == setup.VariantId && x.WarehouseId == setup.WarehouseId)
                .SumAsync(x => x.Quantity);
            var item = await db.SaleItems.SingleAsync(x => x.SaleId == saleId);
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerReturnCommand(
                    setup.WarehouseId,
                    [new CustomerReturnLineInput(item.VariantId, 1m, item.Id, null, "Nuqsonli",
                        ReturnItemCondition.Defective, InventoryDisposition.SupplierClaim)],
                    customerId));
        }

        using var check = Fixture.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stockAfter = await verify.Stocks
            .Where(x => x.VariantId == setup.VariantId && x.WarehouseId == setup.WarehouseId)
            .SumAsync(x => x.Quantity);
        Assert.Equal(stockBefore, stockAfter);

        var claimed = await verify.InventoryPositions
            .Where(x => x.VariantId == setup.VariantId
                        && x.LocationKind == InventoryLocationKind.SupplierClaim
                        && x.LocationId == setup.WarehouseId)
            .SumAsync(x => x.Quantity);
        Assert.Equal(1m, claimed);
    }
}
