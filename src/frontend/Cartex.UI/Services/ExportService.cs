using System.Globalization;
using System.Text;
using Cartex.ApiClient.Api;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cartex.UI.Services;

public enum ExportFormat { Csv, Excel, Pdf }

public sealed record ExportColumn<T>(string Header, Func<T, object?> Value);

public sealed record CompanyHeader(string Name, string? BranchName, string? Contact, byte[]? Logo);

public interface IExportService
{
    Task ExportAsync<T>(string title, IReadOnlyList<T> rows, IReadOnlyList<ExportColumn<T>> columns, ExportFormat format);
}

public sealed class ExportService(IFilePickerService picker, IToastService toast, IBusinessApi businessApi, IStorageApi storageApi, BranchContextService branch) : IExportService
{
    private static readonly HttpClient _http = new();
    private const string NumberFormat = "#,##0.##";
    private const string BrandColor = "#166534";
    private const string ZebraColor = "#F3F4F6";

    static ExportService() => QuestPDF.Settings.License = LicenseType.Community;

    public async Task ExportAsync<T>(string title, IReadOnlyList<T> rows, IReadOnlyList<ExportColumn<T>> columns, ExportFormat format)
    {
        var ext = format switch { ExportFormat.Csv => "csv", ExportFormat.Excel => "xlsx", _ => "pdf" };
        var company = format == ExportFormat.Pdf ? await LoadCompanyAsync() : null;
        var stream = await picker.SaveFileAsync($"{Sanitize(title)}-{DateTime.Now:yyyyMMdd-HHmm}", ext);
        if (stream is null) return;

        try
        {
            await using (stream)
            {
                switch (format)
                {
                    case ExportFormat.Csv: WriteCsv(stream, columns, rows); break;
                    case ExportFormat.Excel: WriteExcel(stream, title, columns, rows); break;
                    default: WritePdf(stream, title, columns, rows, company); break;
                }
            }
            toast.Success(LocalizationManager.Instance["export_done"]);
        }
        catch { toast.Error(LocalizationManager.Instance["error"]); }
    }

    private async Task<CompanyHeader?> LoadCompanyAsync()
    {
        try
        {
            var b = await businessApi.GetAsync();
            var contact = string.Join("  ·  ", new[] { b.Address, b.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
            byte[]? logo = null;
            if (!string.IsNullOrEmpty(b.LogoImageKey))
            {
                try
                {
                    var url = (await storageApi.GetUrlAsync(b.LogoImageKey)).Url;
                    logo = await _http.GetByteArrayAsync(url);
                }
                catch { }
            }
            return new CompanyHeader(b.Name, branch.SelectedBranch?.Name, string.IsNullOrWhiteSpace(contact) ? null : contact, logo);
        }
        catch { return null; }
    }

    private static void WriteCsv<T>(Stream stream, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(true));
        writer.WriteLine(string.Join(",", columns.Select(c => Escape(c.Header))));
        foreach (var row in rows)
            writer.WriteLine(string.Join(",", columns.Select(c => Escape(Display(c.Value(row))))));
    }

    private static void WriteExcel<T>(Stream stream, string title, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var brand = XLColor.FromHtml(BrandColor);
        var zebra = XLColor.FromHtml(ZebraColor);
        var numeric = columns.Select(c => IsNumericColumn(c, rows)).ToArray();

        using var wb = new XLWorkbook();
        var name = Sanitize(title);
        var ws = wb.AddWorksheet(name.Length == 0 ? "Sheet1" : name[..Math.Min(31, name.Length)]);

        var titleRange = ws.Range(1, 1, 1, columns.Count);
        titleRange.Merge();
        titleRange.FirstCell().Value = title;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;

        for (var c = 0; c < columns.Count; c++)
        {
            var cell = ws.Cell(2, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = brand;
            cell.Style.Alignment.Horizontal = numeric[c] ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(r + 3, c + 1);
                SetCell(cell, columns[c].Value(rows[r]));
                if (numeric[c]) cell.Style.NumberFormat.Format = NumberFormat;
                if (r % 2 == 1) cell.Style.Fill.BackgroundColor = zebra;
            }

        if (rows.Count > 0 && numeric.Any(n => n))
        {
            var totalRow = rows.Count + 3;
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(totalRow, c + 1);
                cell.Style.Font.Bold = true;
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                if (numeric[c])
                {
                    cell.Value = rows.Sum(row => ToDecimal(columns[c].Value(row)));
                    cell.Style.NumberFormat.Format = NumberFormat;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }
            var labelCol = Array.IndexOf(numeric, false);
            if (labelCol >= 0) ws.Cell(totalRow, labelCol + 1).Value = LocalizationManager.Instance["total"];
        }

        ws.SheetView.FreezeRows(2);
        ws.Columns().AdjustToContents();
        wb.SaveAs(stream);
    }

    private static void WritePdf<T>(Stream stream, string title, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows, CompanyHeader? company)
    {
        var numeric = columns.Select(c => IsNumericColumn(c, rows)).ToArray();
        var labelCol = Array.IndexOf(numeric, false);

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(9));
                page.Header().Column(header =>
                {
                    if (company is not null)
                    {
                        header.Item().Row(row =>
                        {
                            if (company.Logo is not null)
                                row.ConstantItem(54).Height(54).Image(company.Logo).FitArea();
                            row.RelativeItem().PaddingLeft(company.Logo is not null ? 10 : 0).Column(info =>
                            {
                                info.Item().Text(company.Name).FontSize(14).Bold();
                                if (!string.IsNullOrWhiteSpace(company.BranchName))
                                    info.Item().Text(company.BranchName).FontSize(10).FontColor(Colors.Grey.Darken2);
                                if (!string.IsNullOrWhiteSpace(company.Contact))
                                    info.Item().Text(company.Contact).FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(160).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        header.Item().PaddingTop(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                    }
                    header.Item().PaddingTop(8).PaddingBottom(8).Text(title).FontSize(15).SemiBold();
                });
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        foreach (var _ in columns) def.RelativeColumn();
                    });

                    for (var c = 0; c < columns.Count; c++)
                    {
                        var cell = table.Cell().Background(BrandColor).Padding(5);
                        (numeric[c] ? cell.AlignRight() : cell).Text(columns[c].Header).FontColor(Colors.White).SemiBold();
                    }

                    for (var r = 0; r < rows.Count; r++)
                    {
                        var bg = r % 2 == 1 ? ZebraColor : "#FFFFFF";
                        for (var c = 0; c < columns.Count; c++)
                        {
                            var cell = table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
                            (numeric[c] ? cell.AlignRight() : cell)
                                .Text(numeric[c] ? FormatNumber(columns[c].Value(rows[r])) : Display(columns[c].Value(rows[r])));
                        }
                    }

                    if (rows.Count > 0 && numeric.Any(n => n))
                        for (var c = 0; c < columns.Count; c++)
                        {
                            var cell = table.Cell().BorderTop(1).BorderColor(Colors.Grey.Medium).Padding(5);
                            var text = numeric[c]
                                ? rows.Sum(row => ToDecimal(columns[c].Value(row))).ToString(NumberFormat, CultureInfo.InvariantCulture)
                                : (c == labelCol ? LocalizationManager.Instance["total"] : "");
                            (numeric[c] ? cell.AlignRight() : cell).Text(text).SemiBold();
                        }
                });
                page.Footer().AlignRight().Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
            });
        }).GeneratePdf(stream);
    }

    private static void SetCell(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null: break;
            case string s: cell.Value = s; break;
            case bool b: cell.Value = b; break;
            case DateTime dt: cell.Value = dt; break;
            case DateTimeOffset dto: cell.Value = dto.LocalDateTime; break;
            case decimal d: cell.Value = d; break;
            case double db: cell.Value = db; break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            default: cell.Value = v.ToString() ?? ""; break;
        }
    }

    private static bool IsNumericColumn<T>(ExportColumn<T> col, IReadOnlyList<T> rows)
    {
        foreach (var row in rows)
        {
            var v = col.Value(row);
            if (v is null) continue;
            return v is decimal or double or int or long or float;
        }
        return false;
    }

    private static decimal ToDecimal(object? v) => v switch
    {
        decimal d => d,
        double db => (decimal)db,
        float f => (decimal)f,
        int i => i,
        long l => l,
        _ => 0
    };

    private static string FormatNumber(object? v) =>
        v is null ? "" : ToDecimal(v).ToString(NumberFormat, CultureInfo.InvariantCulture);

    private static string Display(object? v) => v switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
        DateTimeOffset dto => dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        double db => db.ToString("0.##", CultureInfo.InvariantCulture),
        bool b => b ? "+" : "",
        _ => v.ToString() ?? ""
    };

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;

    private static string Sanitize(string s) =>
        string.Concat(s.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch))).Trim();
}
