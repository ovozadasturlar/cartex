using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Import;

public static class ProductImportMatcher
{
    public const int MaxRows = 5000;

    private sealed record Match(long VariantId, string Name);

    public static async Task<List<ImportRowDto>> ResolveAsync(IApplicationDbContext db, List<ImportRowDto> rows, CancellationToken cancellationToken)
    {
        await MatchAsync(db, rows, cancellationToken);
        Validate(rows);
        return rows;
    }

    public static async Task MatchAsync(IApplicationDbContext db, List<ImportRowDto> rows, CancellationToken cancellationToken)
    {
        var codes = rows.Where(r => r.Barcode is not null).Select(r => r.Barcode!).Distinct().ToList();
        Dictionary<string, Match> byBarcode = codes.Count == 0 ? [] : (await db.Barcodes
                .Where(b => codes.Contains(b.Code))
                .Select(b => new { b.Code, b.VariantId, Name = b.Variant.Product.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Code, b => new Match(b.VariantId, b.Name));

        var skus = rows.Where(r => r.Sku is not null).Select(r => r.Sku!).Distinct().ToList();
        Dictionary<string, Match> bySku = skus.Count == 0 ? [] : (await db.ProductVariants
                .Where(v => v.Code != null && skus.Contains(v.Code))
                .Select(v => new { Code = v.Code!, v.Id, Name = v.Product.Name })
                .ToListAsync(cancellationToken))
            .GroupBy(v => v.Code)
            .ToDictionary(g => g.Key, g => new Match(g.First().Id, g.First().Name));

        var names = rows.Where(r => r.Name is not null).Select(r => r.Name!.ToLowerInvariant()).Distinct().ToList();
        var byName = (await db.Products
                .Where(p => names.Contains(p.Name.ToLower()))
                .Select(p => new { p.Name, VariantId = p.Variants.Where(v => v.IsDefault).Select(v => v.Id).FirstOrDefault() })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.Name.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var units = (await db.Units.Where(u => u.IsEnabled).Select(u => new { u.Name, u.ShortName }).ToListAsync(cancellationToken))
            .SelectMany(u => new[] { u.Name, u.ShortName })
            .Select(u => u.ToLowerInvariant())
            .ToHashSet();

        var categories = (await db.Categories.Select(c => c.Name).ToListAsync(cancellationToken))
            .Select(c => c.ToLowerInvariant())
            .ToHashSet();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (row.Unit is { } unit && !units.Contains(unit.ToLowerInvariant()))
                row.Warnings.Add($"Birlik topilmadi: {unit} — asosiy birlik ishlatiladi.");

            if (row.Category is { } category && !categories.Contains(category.Trim().ToLowerInvariant()))
                row.Warnings.Add($"Yangi kategoriya: {category}");

            Match? match = null;
            if (row.Barcode is { } code && byBarcode.TryGetValue(code, out var barcodeMatch))
                match = barcodeMatch;
            else if (row.Sku is { } sku && bySku.TryGetValue(sku, out var skuMatch))
                match = skuMatch;
            else if (row.Name is { } name && byName.TryGetValue(name.ToLowerInvariant(), out var nameMatches))
            {
                if (nameMatches.Count > 1)
                {
                    row.Errors.Add($"Bu nom bilan {nameMatches.Count} ta mahsulot bor — barkod yoki artikul kerak.");
                    continue;
                }
                row.Warnings.Add("Nom bo'yicha mavjud mahsulotga bog'landi.");
                match = new Match(nameMatches[0].VariantId, nameMatches[0].Name);
            }

            if (match is null)
                continue;

            if (row.Name is { } rowName && !string.Equals(rowName, match.Name, StringComparison.OrdinalIgnoreCase))
                row.Warnings.Add($"Mavjud mahsulot: {match.Name}");

            if (row.Barcode is { } given && !byBarcode.ContainsKey(given))
                row.Warnings.Add($"Barkod mavjud mahsulotga qo'shiladi: {given}");

            rows[i] = row with { VariantId = match.VariantId, Action = ImportRowAction.Existing };
        }
    }

    private static void Validate(List<ImportRowDto> rows)
    {
        var duplicateBarcodes = Duplicates(rows.Where(r => !string.IsNullOrWhiteSpace(r.Barcode)), r => r.Barcode!.Trim());
        var duplicateSkus = Duplicates(rows.Where(r => r.Action == ImportRowAction.Create && !string.IsNullOrWhiteSpace(r.Sku)), r => r.Sku!.Trim());
        var duplicateNames = Duplicates(rows.Where(r => r.Action == ImportRowAction.Create && !string.IsNullOrWhiteSpace(r.Name)), r => r.Name!.Trim());

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (string.IsNullOrWhiteSpace(row.Name))
                row.Errors.Add("Mahsulot nomi bo'sh.");
            if (!string.IsNullOrWhiteSpace(row.Barcode) && duplicateBarcodes.Contains(row.Barcode.Trim()))
                row.Errors.Add($"Fayl ichida takroriy barkod: {row.Barcode}");
            if (row.Action == ImportRowAction.Create && !string.IsNullOrWhiteSpace(row.Sku) && duplicateSkus.Contains(row.Sku.Trim()))
                row.Errors.Add($"Fayl ichida takroriy artikul: {row.Sku}");
            if (row.Action == ImportRowAction.Create && !string.IsNullOrWhiteSpace(row.Name) && duplicateNames.Contains(row.Name.Trim()))
                row.Errors.Add($"Fayl ichida takroriy nom: {row.Name.Trim()}");
            if (row.Quantity < 0 || row.MinStock < 0)
                row.Errors.Add("Son manfiy bo'lishi mumkin emas.");
            if (row.SellingPrice < 0 || row.PurchasePrice < 0)
                row.Errors.Add("Narx manfiy bo'lishi mumkin emas.");
            if (row.Vat is { } vat && (vat < 0 || vat > 100))
                row.Errors.Add("QQS 0 va 100 orasida bo'lishi kerak.");
            if (row.Barcode?.Length > 60 || row.Sku?.Length > 60)
                row.Errors.Add("Barkod yoki artikul 60 belgidan uzun.");
            if (row.Ikpu?.Length > 30)
                row.Errors.Add("IKPU 30 belgidan uzun.");

            if (row.Errors.Count > 0)
                rows[i] = row with { Action = ImportRowAction.Skip };
        }
    }

    private static HashSet<string> Duplicates(IEnumerable<ImportRowDto> rows, Func<ImportRowDto, string> key) =>
        rows.GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
