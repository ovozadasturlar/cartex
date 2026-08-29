using System.Globalization;
using Cartex.Catalog.Tool.Normalization;
using ClosedXML.Excel;

namespace Cartex.Catalog.Tool.Io;

public static class WorkbookReader
{
    public const string Source = "sheet";
    public const string SheetName = "Mahsulotlar";

    private const string NameColumn = "Nomi";
    private const string ManufacturerColumn = "Ishlab chiqaruvchi";
    private const string BarcodeColumn = "Barkod";
    private const string PackQuantityColumn = "Pachka soni";
    private const string ModelColumn = "Artikul";
    private const string CategoryColumn = "Kategoriya";
    private const string UnitColumn = "Birlik";
    private const string ImageUrlColumn = "Surat URL";

    private static readonly string[] RequiredColumns = [NameColumn, BarcodeColumn];

    public static IReadOnlyList<SourceRow> Read(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.FirstOrDefault(candidate => candidate.Name.Trim().Equals(SheetName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"Worksheet '{SheetName}' was not found in the workbook.");

        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var headers = new Dictionary<int, string>();
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = Text(sheet.Cell(1, column));
            if (header.Length > 0)
                headers[column] = header;
        }

        var missing = RequiredColumns.Where(required => !headers.Values.Contains(required, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Worksheet '{SheetName}' is missing required column(s): {string.Join(", ", missing)}.");

        var rows = new List<SourceRow>();
        foreach (var row in sheet.Rows(2, lastRow))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (column, header) in headers)
                values[header] = Text(row.Cell(column));

            if (values.Values.Any(value => value.Length > 0))
                rows.Add(Map(row.RowNumber(), values));
        }

        return rows;
    }

    private static SourceRow Map(int rowNumber, Dictionary<string, string> values)
    {
        string Value(string column) => values.GetValueOrDefault(column, string.Empty);

        return new SourceRow(
            Source,
            rowNumber,
            Value(BarcodeColumn),
            Value(NameColumn),
            Value(ManufacturerColumn),
            CategoryPath.Split(Value(CategoryColumn)),
            Value(ModelColumn),
            Value(UnitColumn),
            Value(PackQuantityColumn),
            ImageReference.FromUrl(Value(ImageUrlColumn)));
    }

    private static string Text(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetValue<decimal>().ToString(CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean().ToString(CultureInfo.InvariantCulture),
        XLDataType.Blank => string.Empty,
        _ => cell.GetString().Trim()
    };
}
