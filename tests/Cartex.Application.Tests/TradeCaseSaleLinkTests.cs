using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Application.TradeCases.Commands;
using Cartex.Application.TradeCases.Queries;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class TradeCaseSaleLinkTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long warehouseId, long variantId, decimal price, long customerId)> SetupAsync()
    {
        long branchId;
        long warehouseId;
        long variantId;
        decimal price;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).FirstAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).FirstAsync();
            var businessId = await db.Businesses.Select(x => x.Id).FirstAsync();
            var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).FirstAsync();
            variantId = await db.ProductVariants
                .Where(x => x.Product.Name == "Smesitel oshxona Zegor").Select(x => x.Id).FirstAsync();
            price = await db.ProductPrices.Where(x => x.VariantId == variantId)
                .Select(x => x.SellingPrice).FirstAsync();
            Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        }
        await TestShift.OpenAsync(Fixture);
        using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISettingsService>().SetAsync(
                SettingKeys.TradeCases,
                new TradeCaseSettings
                {
                    Enabled = true,
                    SingularLabel = "Loyiha",
                    PluralLabel = "Loyihalar",
                    DefaultWorkflow = TradeCaseWorkflow.CustodyUntilSettlement,
                    DefaultPricePolicy = TradeCasePricePolicy.SnapshotAtIssue
                });
        }
        long customerId;
        using (var scope = Fixture.CreateScope())
        {
            customerId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerCommand("Link mijozi", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                    null, 0, CreditLimit: 100_000_000m));
        }
        return (warehouseId, variantId, price, customerId);
    }

    [Fact]
    public async Task Linking_a_completed_sale_attaches_it_and_is_idempotent()
    {
        var (warehouseId, variantId, price, customerId) = await SetupAsync();

        long saleId;
        long caseId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            saleId = (await sender.Send(new CreateSaleCommand(warehouseId, customerId, price * 2, 0, 0,
                [new CreateSaleItemDto(variantId, 2)]))).SaleId;
            caseId = (await sender.Send(new CreateTradeCaseCommand(customerId, warehouseId, "Link case"))).Id;
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new LinkSaleToTradeCaseCommand(caseId, saleId));
            await sender.Send(new LinkSaleToTradeCaseCommand(caseId, saleId));
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(caseId, await db.Sales.Where(x => x.Id == saleId).Select(x => x.TradeCaseId).SingleAsync());
            Assert.Equal(2, await db.TradeCases.Where(x => x.Id == caseId).Select(x => x.Version).SingleAsync());

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new UnlinkSaleFromTradeCaseCommand(caseId, saleId));
            Assert.Null(await db.Sales.Where(x => x.Id == saleId).Select(x => x.TradeCaseId).SingleAsync());

            await Assert.ThrowsAsync<ConflictException>(() =>
                sender.Send(new LinkSaleToTradeCaseCommand(caseId, saleId, ExpectedVersion: 2)));
            await sender.Send(new LinkSaleToTradeCaseCommand(caseId, saleId, ExpectedVersion: 3));
        }
    }

    [Fact]
    public async Task Linking_rejects_wrong_customer_and_closed_case()
    {
        var (warehouseId, variantId, price, customerId) = await SetupAsync();

        long otherCustomerId;
        using (var scope = Fixture.CreateScope())
        {
            otherCustomerId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateCustomerCommand("Boshqa mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                    null, 0, CreditLimit: 100_000_000m));
        }

        long caseId;
        long foreignSaleId;
        long ownSaleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            caseId = (await sender.Send(new CreateTradeCaseCommand(customerId, warehouseId, "Strict case"))).Id;
            foreignSaleId = (await sender.Send(new CreateSaleCommand(warehouseId, otherCustomerId, price, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]))).SaleId;
            ownSaleId = (await sender.Send(new CreateSaleCommand(warehouseId, customerId, price, 0, 0,
                [new CreateSaleItemDto(variantId, 1)]))).SaleId;
        }

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<ConflictException>(() =>
                sender.Send(new LinkSaleToTradeCaseCommand(caseId, foreignSaleId)));

            await sender.Send(new LinkSaleToTradeCaseCommand(caseId, ownSaleId));
            await sender.Send(new ChangeTradeCaseStatusCommand(caseId, TradeCaseStatus.Settled));

            await Assert.ThrowsAsync<ConflictException>(() =>
                sender.Send(new UnlinkSaleFromTradeCaseCommand(caseId, ownSaleId)));
        }
    }

    [Fact]
    public async Task Issue_print_query_returns_full_document_content()
    {
        var (warehouseId, variantId, price, customerId) = await SetupAsync();

        long issueId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var caseId = (await sender.Send(new CreateTradeCaseCommand(customerId, warehouseId, "Print case"))).Id;
            issueId = (await sender.Send(new CreateGoodsIssueCommand(caseId,
                [new GoodsIssueLineInput(variantId, 2m, price)], Note: "Sinov eslatmasi"))).Id;
        }

        using var check = Fixture.CreateScope();
        var print = await check.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetGoodsIssuePrintQuery(issueId));

        Assert.Equal(issueId, print.IssueId);
        Assert.Equal("Print case", print.CaseTitle);
        Assert.Equal("Link mijozi", print.CustomerName);
        Assert.False(string.IsNullOrWhiteSpace(print.DocumentNumber));
        Assert.False(string.IsNullOrWhiteSpace(print.SellerName));
        Assert.False(string.IsNullOrWhiteSpace(print.WarehouseName));
        Assert.Equal("Sinov eslatmasi", print.Note);
        var line = Assert.Single(print.Lines);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(price, line.UnitPrice);
        Assert.Equal(price * 2, line.Amount);
        Assert.Equal(price * 2, print.TotalAmount);
    }

    [Fact]
    public async Task Settlement_sale_cannot_be_unlinked()
    {
        var (warehouseId, variantId, price, customerId) = await SetupAsync();

        long caseId;
        long settledSaleId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            caseId = (await sender.Send(new CreateTradeCaseCommand(customerId, warehouseId, "Settle case"))).Id;
            await sender.Send(new CreateGoodsIssueCommand(caseId,
                [new GoodsIssueLineInput(variantId, 2m, price)]));
            settledSaleId = (await sender.Send(new SettleTradeCaseCommand(caseId, price, 0, 0,
                Lines: [new TradeCaseSettlementLineInput(
                    await GetIssueLineIdAsync(scope.ServiceProvider, caseId), 1m)],
                ApplyAutoDiscount: false))).SaleId;
        }

        using var check = Fixture.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(TradeCaseStatus.Open,
            await db.TradeCases.Where(x => x.Id == caseId).Select(x => x.Status).SingleAsync());
        var checker = check.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            checker.Send(new UnlinkSaleFromTradeCaseCommand(caseId, settledSaleId)));
    }

    private async Task<long> GetIssueLineIdAsync(IServiceProvider services, long caseId) =>
        await services.GetRequiredService<ApplicationDbContext>().GoodsIssueLines
            .Where(x => x.Document.TradeCaseId == caseId).Select(x => x.Id).FirstAsync();
}
