using System.Globalization;
using Cartex.Application.Common.Interfaces;
using ClosedXML.Excel;

namespace Cartex.Infrastructure.Import;

public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    public IReadOnlyList<IReadOnlyList<string>> Read(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.FirstOrDefault();
        var used = sheet?.RangeUsed();
        if (used is null)
            return [];

        var columns = used.ColumnCount();
        return used.Rows()
            .Select(row => (IReadOnlyList<string>)[.. Enumerable.Range(1, columns).Select(i => Text(row.Cell(i)))])
            .ToList();
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
