using System.Globalization;
using Cartex.Application.Common.Interfaces;
using ClosedXML.Excel;

namespace Cartex.Infrastructure.Import;

public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    public IReadOnlyList<IReadOnlyList<string>> Read(Stream content)
    {
        // Load the incoming stream into a seekable memory stream
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        memory.Position = 0;
        using var workbook = new XLWorkbook(memory);
        // Possible sheet names that may contain the product data (case‑insensitive)
        var allowedNames = new[] { "mahsulotlar", "mahsulot", "maxsulot", "maxsulotlar", "product", "products" };
        // Prefer a sheet whose name matches one of the allowed names and has data.
        var headerSheet = workbook.Worksheets.FirstOrDefault(ws =>
            ws.RowsUsed().Any() &&
            allowedNames.Any(name => string.Equals(ws.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)));
        // Fallback to the first non‑empty sheet if none of the names match.
        var firstSheet = headerSheet ?? workbook.Worksheets.FirstOrDefault(ws => ws.RowsUsed().Any());
        if (firstSheet is null)
            return [];
        var rows = new List<IReadOnlyList<string>>();
        // Extract rows using RowsUsed which includes all rows with data
        var allRows = firstSheet.RowsUsed().ToArray();
        if (allRows.Length == 0)
            return [];
        // Header row is the first used row
        var headerRow = allRows[0];
        int maxColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 1;
        
        var headerCells = new List<string>();
        for (int c = 1; c <= maxColumn; c++)
        {
            headerCells.Add(Text(headerRow.Cell(c)));
        }
        rows.Add(headerCells);
        // Data rows
        foreach (var row in allRows.Skip(1))
        {
            var cells = new List<string>();
            for (int c = 1; c <= maxColumn; c++)
            {
                cells.Add(Text(row.Cell(c)));
            }
            rows.Add(cells);
        }
        // Process any additional worksheets that also contain data
        foreach (var ws in workbook.Worksheets.Skip(1))
        {
            var wsRows = ws.RowsUsed().ToArray();
            if (wsRows.Length <= 1) continue; // No data beyond header
            // Assume first row is header; skip it
            foreach (var row in wsRows.Skip(1))
            {
                var cells = new List<string>();
                for (int c = 1; c <= maxColumn; c++)
                {
                    cells.Add(Text(row.Cell(c)));
                }
                rows.Add(cells);
            }
        }
        return rows;
    }

    public byte[] Write(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#166534");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < rows[r].Count; c++)
                sheet.Cell(r + 2, c + 1).SetValue(rows[r][c]);

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string Text(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetValue<decimal>().ToString(CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean().ToString(),
        XLDataType.Blank => string.Empty,
        _ => cell.GetString().Trim()
    };
}
