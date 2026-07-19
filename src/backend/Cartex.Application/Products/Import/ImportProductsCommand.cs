using Cartex.Application.Barcodes.Commands;
using Cartex.Application.Products.Commands;
using Cartex.Application.Stocks.Commands;
using Cartex.Application.Supplies.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Import;

public record ImportProductsCommand(
    List<ImportRowDto> Rows,
    ImportStockMode StockMode = ImportStockMode.None,
    long? WarehouseId = null,
    long? SupplierId = null,
    DateOnly? SupplyDate = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    string? Currency = null,
    bool UpdatePrices = false,
    bool CreateMissingCategories = true) : ICommand<ImportResultDto>;

public sealed class ImportProductsCommandHandler(IApplicationDbContext db, ISender sender, ICurrentUser currentUser)
    : IRequestHandler<ImportProductsCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.StockMode != ImportStockMode.None && request.WarehouseId is null)
            throw new BusinessRuleException("Ombor tanlanmagan.");

        if (request.StockMode == ImportStockMode.Supply)
        {
            if (!currentUser.HasPermission(AppPermissions.Supplies.Manage))
                throw new ForbiddenException("Ta'minot kirimi uchun ruxsat yo'q.");
            if (request.SupplierId is null)
                throw new BusinessRuleException("Ta'minotchi tanlanmagan.");
        }

        if (request.StockMode == ImportStockMode.Opening && !currentUser.HasPermission(AppPermissions.Stocks.Manage))
            throw new ForbiddenException("Zaxirani o'zgartirish uchun ruxsat yo'q.");

        var resolved = await ProductImportMatcher.ResolveAsync(
            db,
            [.. request.Rows.Select(r => r with { VariantId = null, Action = ImportRowAction.Create, Errors = [], Warnings = [] })],
            cancellationToken);

        var rows = resolved.Where(r => r.Action != ImportRowAction.Skip).ToList();
        if (rows.Count == 0)
            throw new BusinessRuleException("Import uchun yaroqli qator yo'q.");

        var units = await db.Units.Where(u => u.IsEnabled).ToListAsync(cancellationToken);
        var defaultUnit = units.FirstOrDefault(u => u.IsDefault) ?? units.FirstOrDefault()
            ?? throw new BusinessRuleException("Birlik topilmadi.");
        var unitByName = units
            .SelectMany(u => new[] { (Key: u.Name, u.Id), (Key: u.ShortName, u.Id) })
            .GroupBy(u => u.Key.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        var categoryByName = (await db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken))
            .GroupBy(c => c.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);

        if (request.CreateMissingCategories)
        {
            var missing = rows
                .Where(r => r.Category is not null)
                .Select(r => r.Category!.Trim())
                .Where(c => !categoryByName.ContainsKey(c.ToLowerInvariant()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new Category { Name = name })
                .ToList();

            if (missing.Count > 0)
            {
                if (!currentUser.HasPermission(AppPermissions.Categories.Manage))
                    throw new ForbiddenException("Yangi kategoriya yaratish uchun ruxsat yo'q.");

                db.Categories.AddRange(missing);
                await db.SaveChangesAsync(cancellationToken);
                foreach (var category in missing)
                    categoryByName[category.Name.ToLowerInvariant()] = category.Id;
            }
        }

        long UnitId(string? name) =>
            name is not null && unitByName.TryGetValue(name.ToLowerInvariant(), out var id) ? id : defaultUnit.Id;

        long? CategoryId(string? name) =>
            name is not null && categoryByName.TryGetValue(name.Trim().ToLowerInvariant(), out var id) ? id : null;

        var variants = new Dictionary<int, long>();
        var newProducts = new Dictionary<int, long>();

        foreach (var row in rows)
        {
            if (row.Action == ImportRowAction.Existing)
            {
                variants[row.Row] = row.VariantId!.Value;
                continue;
            }

            newProducts[row.Row] = await sender.Send(new CreateProductCommand(
                Name: row.Name!.Trim(),
                CategoryId: CategoryId(row.Category),
                UnitId: UnitId(row.Unit),
                MinStock: row.MinStock,
                Barcodes: row.Barcode is { } code ? [new BarcodeInput(code, row.PackQty ?? 1)] : null,
                Code: row.Sku,
                IkpuCode: row.Ikpu,
                VatRate: row.Vat,
                SellingPrice: row.SellingPrice), cancellationToken);
        }

        if (newProducts.Count > 0)
        {
            var productIds = newProducts.Values.ToList();
            var defaults = (await db.ProductVariants
                    .Where(v => productIds.Contains(v.ProductId) && v.IsDefault)
                    .Select(v => new { v.ProductId, v.Id })
                    .ToListAsync(cancellationToken))
                .ToDictionary(v => v.ProductId, v => v.Id);

            foreach (var (row, productId) in newProducts)
                variants[row] = defaults[productId];
        }

        var attached = rows
            .Where(r => r.Action == ImportRowAction.Existing && r.Barcode is not null)
            .Select(r => (VariantId: variants[r.Row], Code: r.Barcode!, PackQty: r.PackQty ?? 1))
            .DistinctBy(b => b.Code)
            .ToList();

        if (attached.Count > 0)
        {
            var codes = attached.Select(b => b.Code).ToList();
            var known = await db.Barcodes.Where(b => codes.Contains(b.Code)).Select(b => b.Code).ToListAsync(cancellationToken);

            foreach (var barcode in attached.Where(b => !known.Contains(b.Code)))
                await sender.Send(new CreateBarcodeCommand(barcode.VariantId, barcode.Code, barcode.PackQty), cancellationToken);
        }

        var variantIds = variants.Values.Distinct().ToList();
        var withBarcode = await db.Barcodes
            .Where(b => variantIds.Contains(b.VariantId))
            .Select(b => b.VariantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var packByVariant = rows
            .GroupBy(r => variants[r.Row])
            .ToDictionary(g => g.Key, g => g.Select(r => r.PackQty).FirstOrDefault(p => p > 1) ?? 1);

        var generated = 0;
        foreach (var variantId in variantIds.Except(withBarcode))
        {
            await sender.Send(new GenerateBarcodeCommand(variantId, packByVariant[variantId]), cancellationToken);
            generated++;
        }

        if (request.UpdatePrices)
        {
            foreach (var row in rows.Where(r => r.Action == ImportRowAction.Existing && r.SellingPrice is not null))
                await ProductPriceWriter.UpsertAsync(db, variants[row.Row], null, row.SellingPrice!.Value, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        long? supplyId = null;
        var adjusted = 0;
        var stockRows = rows.Where(r => r.Quantity > 0).ToList();

        if (request.StockMode != ImportStockMode.None && stockRows.Count == 0)
            throw new BusinessRuleException("Miqdor ustuni topilmadi yoki barcha miqdorlar bo'sh.");

        if (request.StockMode == ImportStockMode.Supply)
        {
            var items = stockRows
                .Select(r => new CreateSupplyItemDto(variants[r.Row], r.Quantity!.Value, r.PurchasePrice ?? 0, r.ExpiredAt))
                .ToList();

            supplyId = await sender.Send(new CreateSupplyCommand(
                request.SupplierId!.Value,
                request.WarehouseId!.Value,
                request.SupplyDate ?? DateOnly.FromDateTime(DateTime.Now),
                items,
                request.PaidCash,
                request.PaidCard,
                request.Currency), cancellationToken);
        }
        else if (request.StockMode == ImportStockMode.Opening)
        {
            foreach (var row in stockRows)
            {
                await sender.Send(new AddOpeningStockCommand(
                    request.WarehouseId!.Value,
                    variants[row.Row],
                    row.Quantity!.Value,
                    row.PurchasePrice ?? 0,
                    row.ExpiredAt), cancellationToken);
                adjusted++;
            }
        }

        return new ImportResultDto(
            newProducts.Count,
            rows.Count(r => r.Action == ImportRowAction.Existing),
            generated,
            supplyId,
            adjusted);
    }
}

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleFor(x => x.Rows).NotEmpty();
        RuleFor(x => x.Rows.Count).LessThanOrEqualTo(ProductImportMatcher.MaxRows);
        RuleFor(x => x.PaidCash).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PaidCard).GreaterThanOrEqualTo(0);
    }
}
