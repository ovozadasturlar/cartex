using Cartex.Application.Common.Messaging;
using Cartex.Application.Products.Commands;
using Cartex.Application.Products.Import;
using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class ProductImportTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private static Stream Sheet(params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");

        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(r + 1, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case string text: cell.Value = text; break;
                    case decimal number: cell.Value = number; break;
                    default: cell.Value = rows[r][c]!.ToString(); break;
                }
            }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private async Task<long> LoginAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch = (await db.Branches.FirstAsync(b => b.Name == "Filial 1")).Id;
        var warehouse = (await db.Warehouses.FirstAsync(w => w.Name == "Filial 1 ombori")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch);
        return warehouse;
    }

    [Fact]
    public async Task Preview_DetectsColumns_AndMatchesExistingByName()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var file = Sheet(
            ["Nomi", "Barkod", "Kategoriya", "Sotish narxi", "Soni"],
            ["Smesitel oshxona Zegor", null, "Smesitellar", "12000", 5m],
            ["Yangi mahsulot", "4780000000001", "Yangi turkum", "3000", 2m]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));

        Assert.Equal(5, preview.Mapping.Count);
        Assert.Equal(nameof(ImportField.Name), preview.Mapping[0]);
        Assert.Equal(nameof(ImportField.Quantity), preview.Mapping[4]);
        Assert.Equal(1, preview.ExistingCount);
        Assert.Equal(1, preview.CreateCount);
        Assert.Equal(0, preview.ErrorCount);

        var existing = preview.Rows[0];
        Assert.Equal(ImportRowAction.Existing, existing.Action);
        Assert.NotNull(existing.VariantId);
        Assert.Equal(12000m, existing.SellingPrice);

        var created = preview.Rows[1];
        Assert.Equal(ImportRowAction.Create, created.Action);
        Assert.Contains(created.Warnings, w => w.Contains("Yangi turkum"));
    }

    [Fact]
    public async Task Import_GeneratesBarcode_WhenSheetHasNone()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var file = Sheet(
            ["Nomi", "Barkod"],
            ["Barkodsiz mahsulot", null],
            ["Barkodli mahsulot", "4780000000002"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        var result = await sender.Send(new ImportProductsCommand(preview.Rows));

        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.BarcodesGenerated);

        var variantId = await db.ProductVariants
            .Where(v => v.Product.Name == "Barkodsiz mahsulot")
            .Select(v => v.Id)
            .SingleAsync();
        var code = await db.Barcodes.Where(b => b.VariantId == variantId).Select(b => b.Code).SingleAsync();

        Assert.Equal($"CTX-{variantId:D6}", code);
        Assert.True(await db.Barcodes.AnyAsync(b => b.Code == "4780000000002"));
    }

    [Fact]
    public async Task Preview_DuplicateBarcodeInFile_IsError()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var file = Sheet(
            ["Nomi", "Barkod"],
            ["Birinchi", "4780000000003"],
            ["Ikkinchi", "4780000000003"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));

        Assert.Equal(2, preview.ErrorCount);
        Assert.All(preview.Rows, r => Assert.Equal(ImportRowAction.Skip, r.Action));
        await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new ImportProductsCommand(preview.Rows)));
    }

    [Fact]
    public async Task Import_SupplyMode_CreatesSupplyWithStockAndSupplierDebt()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var supplierId = await sender.Send(new CreateSupplierCommand("Import ta'minotchi", null));

        await using var file = Sheet(
            ["Nomi", "Soni", "Kirim narxi"],
            ["Import tovari", 10m, 2500m]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        var result = await sender.Send(new ImportProductsCommand(preview.Rows, ImportStockMode.Supply,
            warehouseId, supplierId));

        Assert.NotNull(result.SupplyId);

        var supply = await db.Supplies.Include(s => s.Items).SingleAsync(s => s.Id == result.SupplyId);
        Assert.Equal(25000m, supply.TotalAmount);
        Assert.Single(supply.Items);

        var stock = await db.Stocks.SingleAsync(s => s.SupplyId == result.SupplyId);
        Assert.Equal(10m, stock.Quantity);
        Assert.Equal(2500m, stock.PurchasePrice);

        var payable = -await db.Accounts
            .Where(a => a.SupplierId == supplierId && a.Type == AccountType.Debt)
            .Select(a => a.Balance)
            .SingleAsync();
        Assert.Equal(25000m, payable);
    }

    [Fact]
    public async Task Import_OpeningMode_CreatesStock_WithoutLedgerMovement()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var transactionsBefore = await db.Transactions.CountAsync();

        await using var file = Sheet(
            ["Nomi", "Soni", "Kirim narxi"],
            ["Boshlang'ich tovar", 40m, 1800m]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        var result = await sender.Send(new ImportProductsCommand(preview.Rows, ImportStockMode.Opening, warehouseId));

        Assert.Null(result.SupplyId);
        Assert.Equal(1, result.StockAdjusted);

        var stock = await db.Stocks
            .SingleAsync(s => s.Variant.Product.Name == "Boshlang'ich tovar" && s.WarehouseId == warehouseId);
        Assert.Equal(40m, stock.Quantity);
        Assert.Equal(1800m, stock.PurchasePrice);
        Assert.Null(stock.SupplyId);

        Assert.Equal(transactionsBefore, await db.Transactions.CountAsync());
        Assert.Equal(0, await db.Supplies.CountAsync());
    }

    [Fact]
    public async Task Import_UnknownUnit_FallsBackToDefault_WithoutCreatingUnit()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var unitsBefore = await db.Units.CountAsync();

        await using var file = Sheet(
            ["Nomi", "Birlik"],
            ["Noma'lum birlikli tovar", "qanaqadir birlik"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        Assert.Contains(preview.Rows[0].Warnings, w => w.Contains("qanaqadir birlik"));

        await sender.Send(new ImportProductsCommand(preview.Rows));

        var defaultUnitId = await db.Units.Where(u => u.IsDefault).Select(u => u.Id).FirstAsync();
        var unitId = await db.Products
            .Where(p => p.Name == "Noma'lum birlikli tovar")
            .Select(p => p.UnitId)
            .SingleAsync();

        Assert.Equal(defaultUnitId, unitId);
        Assert.Equal(unitsBefore, await db.Units.CountAsync());
    }

    [Fact]
    public async Task Import_ExistingProduct_AttachesSheetBarcode_InsteadOfGenerating()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var unitId = await db.Units.Where(u => u.IsDefault).Select(u => u.Id).FirstAsync();
        await sender.Send(new CreateProductCommand(
            Name: "Barkodsiz mavjud", CategoryId: null, UnitId: unitId, MinStock: null, Barcodes: null));

        await using var file = Sheet(
            ["Nomi", "Barkod"],
            ["Barkodsiz mavjud", "4780000000009"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        Assert.Equal(ImportRowAction.Existing, preview.Rows[0].Action);

        var result = await sender.Send(new ImportProductsCommand(preview.Rows));

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Existing);
        Assert.Equal(0, result.BarcodesGenerated);

        var variantId = await db.ProductVariants
            .Where(v => v.Product.Name == "Barkodsiz mavjud")
            .Select(v => v.Id)
            .SingleAsync();
        var code = await db.Barcodes.Where(b => b.VariantId == variantId).Select(b => b.Code).SingleAsync();

        Assert.Equal("4780000000009", code);
    }

    [Fact]
    public async Task Import_OpeningMode_AddsBatch_WithoutWipingExistingStock()
    {
        var warehouseId = await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var first = Sheet(
            ["Nomi", "Soni", "Kirim narxi", "Yaroqlilik muddati"],
            ["Partiyali tovar", 10m, 1000m, "2027-01-31"]);
        var opening = await sender.Send(new PreviewProductImportQuery(first));
        await sender.Send(new ImportProductsCommand(opening.Rows, ImportStockMode.Opening, warehouseId));

        await using var second = Sheet(
            ["Nomi", "Soni", "Kirim narxi", "Yaroqlilik muddati"],
            ["Partiyali tovar", 5m, 1200m, "2028-06-30"]);
        var refill = await sender.Send(new PreviewProductImportQuery(second));
        Assert.Equal(ImportRowAction.Existing, refill.Rows[0].Action);
        await sender.Send(new ImportProductsCommand(refill.Rows, ImportStockMode.Opening, warehouseId));

        var stocks = await db.Stocks
            .Where(s => s.Variant.Product.Name == "Partiyali tovar")
            .OrderBy(s => s.Id)
            .ToListAsync();

        Assert.Equal(2, stocks.Count);
        Assert.Equal(15m, stocks.Sum(s => s.Quantity));
        Assert.Equal(new DateOnly(2027, 1, 31), stocks[0].ExpiredAt);
        Assert.Equal(new DateOnly(2028, 6, 30), stocks[1].ExpiredAt);
        Assert.Equal(1200m, stocks[1].PurchasePrice);
    }

    [Fact]
    public async Task Preview_DuplicateSkuInFile_IsError()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var file = Sheet(
            ["Nomi", "Artikul"],
            ["Birinchi artikul", "SKU-1"],
            ["Ikkinchi artikul", "SKU-1"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));

        Assert.Equal(2, preview.ErrorCount);
        Assert.All(preview.Rows, r => Assert.Equal(ImportRowAction.Skip, r.Action));
    }

    [Fact]
    public async Task Import_RevalidatesRows_IgnoringClientClaims()
    {
        await LoginAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var file = Sheet(
            ["Nomi", "Barkod"],
            ["Soxta A", "4780000000011"],
            ["Soxta B", "4780000000011"]);

        var preview = await sender.Send(new PreviewProductImportQuery(file));
        var tampered = preview.Rows.Select(r => r with { Action = ImportRowAction.Create, Errors = [] }).ToList();

        await Assert.ThrowsAsync<BusinessRuleException>(() => sender.Send(new ImportProductsCommand(tampered)));
        Assert.False(await db.Products.AnyAsync(p => p.Name == "Soxta A"));
    }
}
