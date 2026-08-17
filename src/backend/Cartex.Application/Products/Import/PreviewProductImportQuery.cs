using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Cartex.Shared.Models.Products;

namespace Cartex.Application.Products.Import;

public record PreviewProductImportQuery(Stream Content, string? Mapping = null) : IRequest<ProductImportPreviewDto>;

public sealed class PreviewProductImportQueryHandler(IApplicationDbContext db, ISpreadsheetService spreadsheet)
    : IRequestHandler<PreviewProductImportQuery, ProductImportPreviewDto>
{
    public async Task<ProductImportPreviewDto> Handle(PreviewProductImportQuery request, CancellationToken cancellationToken)
    {
        var table = spreadsheet.Read(request.Content);
        Console.WriteLine($"[Debug] Imported rows (including header): {table.Count}");
        if (table.Count < 2)
            throw new BusinessRuleException("Faylda ma'lumot yo'q.");

        var header = table[0];
        var mapping = request.Mapping is { Length: > 0 } custom ? ImportColumns.Parse(custom) : ImportColumns.Detect(header);
        if (!mapping.ContainsValue(ImportField.Name))
            throw new BusinessRuleException("Mahsulot nomi ustuni topilmadi.");

        var body = table
            .Select((cells, index) => (Cells: cells, Number: index + 1))
            .Skip(1)
            .Where(x => x.Cells.Any(c => !string.IsNullOrWhiteSpace(c)))
            .ToList();

        if (body.Count > ProductImportMatcher.MaxRows)
            throw new BusinessRuleException($"Qatorlar soni {ProductImportMatcher.MaxRows} tadan oshmasligi kerak.");

        var rows = await ProductImportMatcher.ResolveAsync(
            db,
            [.. body.Select(x => ImportColumns.ReadRow(x.Number, x.Cells, mapping))],
            cancellationToken);

        return new ProductImportPreviewDto(
            [.. header],
            mapping.ToDictionary(m => m.Key, m => m.Value.ToString()),
            rows,
            rows.Count(r => r.Action == ImportRowAction.Create),
            rows.Count(r => r.Action == ImportRowAction.Existing),
            rows.Count(r => r.Errors.Count > 0));
    }

}
