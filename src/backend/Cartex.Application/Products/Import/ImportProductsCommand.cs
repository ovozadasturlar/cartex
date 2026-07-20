using Cartex.Application.Barcodes.Commands;
using Cartex.Application.Common.Images;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Products.Commands;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Import;

public record ImportProductsCommand(
    List<ImportRowDto> Rows,
    bool UpdatePrices = false,
    bool CreateMissingCategories = true) : ICommand<ImportResultDto>;

public sealed class ImportProductsCommandHandler(IApplicationDbContext db, ISender sender, ICurrentUser currentUser,
    IRemoteImageFetcher imageFetcher, IObjectStorage storage, IImageProcessor imageProcessor)
    : IRequestHandler<ImportProductsCommand, ImportResultDto>
{
    public async Task<ImportResultDto> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
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

        var (imagesSet, imagesFailed) = await AttachImagesAsync(rows, variants, cancellationToken);

        return new ImportResultDto(
            newProducts.Count,
            rows.Count(r => r.Action == ImportRowAction.Existing),
            generated,
            imagesSet,
            imagesFailed);
    }

    private async Task<(int Set, int Failed)> AttachImagesAsync(
        List<ImportRowDto> rows, Dictionary<int, long> variants, CancellationToken cancellationToken)
    {
        var imageRows = rows.Where(r => !string.IsNullOrWhiteSpace(r.ImageUrl)).ToList();
        if (imageRows.Count == 0)
            return (0, 0);

        var variantIds = imageRows.Select(r => variants[r.Row]).Distinct().ToList();
        var productByVariant = (await db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Product })
                .ToListAsync(cancellationToken))
            .ToDictionary(v => v.Id, v => v.Product);

        var set = 0;
        var failed = 0;
        var handled = new HashSet<long>();

        foreach (var row in imageRows)
        {
            var product = productByVariant.GetValueOrDefault(variants[row.Row]);
            if (product is null || !handled.Add(product.Id) || product.ImageKey is not null)
                continue;

            var fetched = await imageFetcher.FetchAsync(row.ImageUrl!.Trim(), cancellationToken);
            if (fetched is null)
            {
                failed++;
                continue;
            }

            using var buffer = new MemoryStream(fetched.Content);
            product.ImageKey = await ImageStore.SaveAsync(storage, imageProcessor, buffer, fetched.ContentType, fetched.Extension, cancellationToken);
            set++;
        }

        if (set > 0)
            await db.SaveChangesAsync(cancellationToken);

        return (set, failed);
    }
}

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleFor(x => x.Rows).NotEmpty();
        RuleFor(x => x.Rows.Count).LessThanOrEqualTo(ProductImportMatcher.MaxRows);
    }
}
