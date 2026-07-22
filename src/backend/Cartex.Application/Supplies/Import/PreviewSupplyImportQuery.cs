using Cartex.Application.Common.Interfaces;
using Cartex.Application.Products.Import;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Supplies.Import;

public record SupplyImportRowDto(
    int Row,
    long? VariantId,
    string? Name,
    string? Barcode,
    decimal Quantity,
    decimal? PurchasePrice,
    decimal? SellingPrice,
    DateOnly? ExpiredAt,
    string? Message);

public record SupplyImportPreviewDto(List<SupplyImportRowDto> Rows, int MatchedCount, int UnmatchedCount);

public record PreviewSupplyImportQuery(Stream Content) : IRequest<SupplyImportPreviewDto>;

public sealed class PreviewSupplyImportQueryHandler(IApplicationDbContext db, ISpreadsheetService spreadsheet)
    : IRequestHandler<PreviewSupplyImportQuery, SupplyImportPreviewDto>
{
    public async Task<SupplyImportPreviewDto> Handle(PreviewSupplyImportQuery request, CancellationToken cancellationToken)
    {
        var table = spreadsheet.Read(request.Content);
        if (table.Count < 2)
            throw new BusinessRuleException("Faylda ma'lumot yo'q.");

        var mapping = ImportColumns.Detect(table[0]);
        if (!mapping.ContainsValue(ImportField.Quantity))
            throw new BusinessRuleException("Miqdor (soni) ustuni topilmadi.");
        if (!mapping.ContainsValue(ImportField.Name) && !mapping.ContainsValue(ImportField.Barcode) && !mapping.ContainsValue(ImportField.Sku))
            throw new BusinessRuleException("Mahsulot ustuni (nomi, barkod yoki artikul) topilmadi.");

        var body = table
            .Select((cells, index) => (Cells: cells, Number: index + 1))
            .Skip(1)
            .Where(x => x.Cells.Any(c => !string.IsNullOrWhiteSpace(c)))
            .ToList();

        if (body.Count > ProductImportMatcher.MaxRows)
            throw new BusinessRuleException($"Qatorlar soni {ProductImportMatcher.MaxRows} tadan oshmasligi kerak.");

        var rows = body.Select(x => ImportColumns.ReadRow(x.Number, x.Cells, mapping)).ToList();
        await ProductImportMatcher.MatchAsync(db, rows, cancellationToken);

        var variantIds = rows.Where(r => r.VariantId is not null).Select(r => r.VariantId!.Value).Distinct().ToList();
        var names = variantIds.Count == 0
            ? []
            : await db.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Product.Name })
                .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);

        var result = rows.Select(r =>
        {
            var matched = r.VariantId is { } variantId && r.Quantity is > 0;
            var message = r.VariantId is null
                ? "Mahsulot topilmadi"
                : r.Quantity is not > 0 ? "Miqdor yo'q yoki noto'g'ri" : null;

            return new SupplyImportRowDto(
                r.Row,
                matched ? r.VariantId : null,
                r.VariantId is { } id && names.TryGetValue(id, out var name) ? name : r.Name,
                r.Barcode,
                r.Quantity ?? 0,
                r.PurchasePrice,
                r.SellingPrice,
                r.ExpiredAt,
                message);
        }).ToList();

        return new SupplyImportPreviewDto(
            result,
            result.Count(r => r.VariantId is not null),
            result.Count(r => r.VariantId is null));
    }
}
