using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;

namespace Cartex.Application.Products.Import;

public record PreviewProductImportQuery(Stream Content, string? Mapping = null) : IRequest<ProductImportPreviewDto>;

public sealed class PreviewProductImportQueryHandler(IApplicationDbContext db, ISpreadsheetService spreadsheet)
    : IRequestHandler<PreviewProductImportQuery, ProductImportPreviewDto>
{
    public async Task<ProductImportPreviewDto> Handle(PreviewProductImportQuery request, CancellationToken cancellationToken)
    {
        var table = spreadsheet.Read(request.Content);
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
            [.. body.Select(x => Read(x.Number, x.Cells, mapping))],
            cancellationToken);

        return new ProductImportPreviewDto(
            [.. header],
            mapping.ToDictionary(m => m.Key, m => m.Value.ToString()),
            rows,
            rows.Count(r => r.Action == ImportRowAction.Create),
            rows.Count(r => r.Action == ImportRowAction.Existing),
            rows.Count(r => r.Errors.Count > 0));
    }

    private static ImportRowDto Read(int number, IReadOnlyList<string> cells, Dictionary<int, ImportField> mapping)
    {
        var values = new Dictionary<ImportField, string>();
        foreach (var (index, field) in mapping)
            if (index >= 0 && index < cells.Count && !string.IsNullOrWhiteSpace(cells[index]))
                values[field] = cells[index].Trim();

        var warnings = new List<string>();

        string? Text(ImportField field) => values.GetValueOrDefault(field);

        decimal? Number(ImportField field)
        {
            if (Text(field) is not { } raw)
                return null;
            var value = ImportColumns.Number(raw);
            if (value is null)
                warnings.Add($"Son o'qilmadi: {raw}");
            return value;
        }

        DateOnly? Date(ImportField field)
        {
            if (Text(field) is not { } raw)
                return null;
            var value = ImportColumns.Date(raw);
            if (value is null)
                warnings.Add($"Sana o'qilmadi: {raw}");
            return value;
        }

        return new ImportRowDto(
            number,
            Text(ImportField.Name),
            Text(ImportField.Barcode),
            Number(ImportField.PackQty),
            Text(ImportField.Sku),
            Text(ImportField.Category),
            Text(ImportField.Unit),
            Number(ImportField.SellingPrice),
            Number(ImportField.PurchasePrice),
            Number(ImportField.Quantity),
            Date(ImportField.ExpiredAt),
            Number(ImportField.MinStock),
            Text(ImportField.Ikpu),
            Number(ImportField.Vat),
            null,
            ImportRowAction.Create,
            [],
            warnings);
    }
}
