using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.TradeCases.Commands;
using Cartex.Application.TradeCases.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.TradeCases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class TradeCaseTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Custody_issue_return_and_settlement_are_exact_and_idempotent()
    {
        long branchId;
        long warehouseId;
        long businessId;
        long adminId;
        long variantId;
        decimal stockBefore;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            variantId = await db.ProductVariants
                .Where(x => x.Product.Name == "Smesitel oshxona Zegor")
                .Select(x => x.Id).FirstAsync();
            stockBefore = await db.Stocks
                .Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId)
                .SumAsync(x => x.Quantity);
        }
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISettingsService>().SetAsync(
                SettingKeys.TradeCases,
                new TradeCaseSettings
                {
                    Enabled = true,
                    SingularLabel = "Obyekt",
                    PluralLabel = "Obyektlar",
                    DefaultWorkflow = TradeCaseWorkflow.CustodyUntilSettlement,
                    DefaultPricePolicy = TradeCasePricePolicy.SnapshotAtIssue
                });
        }

        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            customerId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerCommand("Obyekt mijozi", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                    null, 0, CreditLimit: 100_000_000m));
        }

        TradeCaseCreatedDto created;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new CreateTradeCaseCommand(customerId, warehouseId, "Yangi uy",
                "Toshkent", IdempotencyKey: "case-flow-1");
            created = await sender.Send(command);
            Assert.Equal(created, await sender.Send(command));
        }

        GoodsIssueCreatedDto issued;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new CreateGoodsIssueCommand(created.Id,
                [new GoodsIssueLineInput(variantId, 2m, 10_000m)],
                IdempotencyKey: "case-issue-1", ExpectedCaseVersion: 1);
            issued = await sender.Send(command);
            Assert.Equal(issued, await sender.Send(command));
        }
        Assert.Equal(2, issued.CaseVersion);

        long issueLineId;
        using (var scope = Fixture.CreateScope())
        {
            issueLineId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GoodsIssueLines
                .Where(x => x.GoodsIssueDocumentId == issued.Id).Select(x => x.Id).FirstAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new CreateGoodsReturnCommand(created.Id,
                [new GoodsReturnLineInput(issueLineId, 1m, "Ortiqcha qoldi",
                    ReturnItemCondition.Sellable, InventoryDisposition.SellableRestock)],
                IdempotencyKey: "case-return-1", ExpectedCaseVersion: 2);
            var returned = await sender.Send(command);
            Assert.Equal(returned, await sender.Send(command));
            Assert.Equal(3, returned.CaseVersion);
        }

        TradeCaseSettlementCreatedDto settled;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new SettleTradeCaseCommand(created.Id,
                10_000m, 0, 0,
                ApplyAutoDiscount: false,
                IdempotencyKey: "case-settlement-1",
                ExpectedCaseVersion: 3);
            settled = await sender.Send(command);
            Assert.Equal(settled, await sender.Send(command));
        }

        using var check = Fixture.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(settled.CaseSettled);
        Assert.Equal(10_000m, settled.Amount);
        Assert.Equal(4, settled.CaseVersion);
        Assert.Equal(TradeCaseStatus.Settled,
            await db2.TradeCases.Where(x => x.Id == created.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(stockBefore - 1m, await db2.Stocks
            .Where(x => x.WarehouseId == warehouseId && x.VariantId == variantId)
            .SumAsync(x => x.Quantity));
        Assert.Equal(0m, await db2.InventoryPositions
            .Where(x => x.BranchId == branchId
                        && x.LocationKind == InventoryLocationKind.CustomerCustody
                        && x.LocationId == created.Id
                        && x.VariantId == variantId)
            .Select(x => x.Quantity).SingleAsync());
        var sale = await db2.Sales.Include(x => x.Items).SingleAsync(x => x.Id == settled.SaleId);
        Assert.Equal(created.Id, sale.TradeCaseId);
        Assert.Equal(1m, sale.Items.Sum(x => x.Quantity));
        Assert.Equal(1, await db2.GoodsIssueDocuments.CountAsync(x => x.TradeCaseId == created.Id));
        Assert.Equal(1, await db2.GoodsReturnDocuments.CountAsync(x => x.TradeCaseId == created.Id));
        Assert.Equal(1, await db2.TradeCaseSettlements.CountAsync(x => x.TradeCaseId == created.Id));

        var statement = await check.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetTradeCaseStatementQuery(created.Id));
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(0m, statement.ClosingBalance);
        var product = Assert.Single(statement.Products);
        Assert.Equal(2m, product.Issued);
        Assert.Equal(1m, product.ReturnedSellable);
        Assert.Equal(1m, product.Settled);
        Assert.Equal(0m, product.OutstandingCustody);
    }
}
