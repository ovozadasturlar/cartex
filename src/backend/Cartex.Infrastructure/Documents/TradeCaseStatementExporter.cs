using ClosedXML.Excel;
using Cartex.Application.Common.Interfaces;
using Cartex.Shared.Models.TradeCases;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cartex.Infrastructure.Documents;

public sealed class TradeCaseStatementExporter : ITradeCaseStatementExporter
{
    public TradeCaseStatementExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public GeneratedDocument Export(TradeCaseStatementDto statement, string format, string mode)
    {
        format = format.Trim().ToLowerInvariant();
        mode = NormalizeMode(mode);
        var safeNumber = string.Concat(statement.CaseNumber.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return format switch
        {
            "xlsx" => new GeneratedDocument(Excel(statement, mode),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{safeNumber}-statement.xlsx"),
            "pdf" => new GeneratedDocument(Pdf(statement, mode), "application/pdf",
                $"{safeNumber}-statement.pdf"),
            _ => throw new ArgumentException("Format faqat pdf yoki xlsx bo'lishi mumkin.", nameof(format))
        };
    }

    private static byte[] Excel(TradeCaseStatementDto value, string mode)
    {
        using var workbook = new XLWorkbook();
        if (mode is "timeline" or "both")
        {
            var sheet = workbook.AddWorksheet("Harakatlar");
            Header(sheet, value, "Harakatlar");
            var headers = new[] { "Sana", "Hujjat", "Turi", "Izoh", "Debet", "Kredit", "Qoldiq", "Valyuta" };
            WriteHeaders(sheet, 6, headers);
            var row = 7;
            foreach (var item in value.Timeline)
            {
                sheet.Cell(row, 1).Value = item.OccurredAt.ToLocalTime();
                sheet.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                sheet.Cell(row, 2).Value = item.DocumentNumber;
                sheet.Cell(row, 3).Value = item.Type;
                sheet.Cell(row, 4).Value = item.Summary;
                sheet.Cell(row, 5).Value = item.Debit;
                sheet.Cell(row, 6).Value = item.Credit;
                sheet.Cell(row, 7).Value = item.RunningBalance;
                sheet.Cell(row, 8).Value = item.Currency;
                row++;
            }
            sheet.Cell(row + 1, 6).Value = "Yakuniy qoldiq";
            sheet.Cell(row + 1, 7).Value = value.ClosingBalance;
            Finish(sheet, 8);
        }

        if (mode is "consolidated" or "both")
        {
            var sheet = workbook.AddWorksheet("Yakuniy hisob");
            Header(sheet, value, "Yakuniy hisob");
            var headers = new[] { "Mahsulot", "Birlik", "Berildi", "Yaroqli qaytdi", "Boshqa qaytdi", "Hisoblandi", "Saqlovda", "O'rtacha narx", "Hisob summa" };
            WriteHeaders(sheet, 6, headers);
            var row = 7;
            foreach (var item in value.Products)
            {
                sheet.Cell(row, 1).Value = item.ProductName;
                sheet.Cell(row, 2).Value = item.UnitName;
                sheet.Cell(row, 3).Value = item.Issued;
                sheet.Cell(row, 4).Value = item.ReturnedSellable;
                sheet.Cell(row, 5).Value = item.ReturnedNonSellable;
                sheet.Cell(row, 6).Value = item.Settled;
                sheet.Cell(row, 7).Value = item.OutstandingCustody;
                sheet.Cell(row, 8).Value = item.AverageUnitPrice;
                sheet.Cell(row, 9).Value = item.ChargedAmount;
                row++;
            }
            Finish(sheet, 9);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] Pdf(TradeCaseStatementDto value, string mode) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Column(column =>
            {
                column.Item().Text($"{value.CaseNumber} — {value.CaseTitle}").FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                column.Item().Text($"Mijoz: {value.CustomerName}");
                column.Item().Text($"Davr: {Range(value)} · Valyuta: {value.Currency}").FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingVertical(16).Column(column =>
            {
                if (mode is "timeline" or "both")
                {
                    column.Item().Text("Harakatlar").FontSize(12).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70); c.ConstantColumn(85); c.RelativeColumn();
                            c.ConstantColumn(60); c.ConstantColumn(60); c.ConstantColumn(65);
                        });
                        PdfHeader(table, ["Sana", "Hujjat", "Izoh", "Debet", "Kredit", "Qoldiq"]);
                        foreach (var row in value.Timeline)
                        {
                            Cell(table, row.OccurredAt.ToLocalTime().ToString("dd.MM.yy HH:mm"));
                            Cell(table, row.DocumentNumber);
                            Cell(table, row.Summary);
                            Cell(table, row.Debit == 0 ? "" : row.Debit.ToString("N2"), true);
                            Cell(table, row.Credit == 0 ? "" : row.Credit.ToString("N2"), true);
                            Cell(table, row.RunningBalance.ToString("N2"), true);
                        }
                    });
                    column.Item().PaddingTop(7).AlignRight().Text($"Yakuniy qoldiq: {value.ClosingBalance:N2} {value.Currency}").Bold();
                }

                if (mode == "both") column.Item().PaddingVertical(12).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                if (mode is "consolidated" or "both")
                {
                    column.Item().Text("Yakuniy hisob").FontSize(12).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.ConstantColumn(40); c.ConstantColumn(48);
                            c.ConstantColumn(48); c.ConstantColumn(48); c.ConstantColumn(48);
                            c.ConstantColumn(60);
                        });
                        PdfHeader(table, ["Mahsulot", "Birlik", "Berildi", "Qaytdi", "Hisob", "Saqlov", "Summa"]);
                        foreach (var row in value.Products)
                        {
                            Cell(table, row.ProductName);
                            Cell(table, row.UnitName);
                            Cell(table, row.Issued.ToString("0.###"), true);
                            Cell(table, (row.ReturnedSellable + row.ReturnedNonSellable).ToString("0.###"), true);
                            Cell(table, row.Settled.ToString("0.###"), true);
                            Cell(table, row.OutstandingCustody.ToString("0.###"), true);
                            Cell(table, row.ChargedAmount.ToString("N2"), true);
                        }
                    });
                }
            });
            page.Footer().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).AlignCenter().Text(text =>
            {
                text.Span("Cartex · ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void Header(IXLWorksheet sheet, TradeCaseStatementDto value, string title)
    {
        sheet.Cell(1, 1).Value = $"{value.CaseNumber} — {value.CaseTitle}";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Mijoz: {value.CustomerName}";
        sheet.Cell(3, 1).Value = $"Davr: {Range(value)}";
        sheet.Cell(4, 1).Value = title;
        sheet.Cell(4, 1).Style.Font.Bold = true;
    }

    private static void WriteHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = sheet.Cell(row, index + 1);
            cell.Value = headers[index];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#166534");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Font.Bold = true;
        }
    }

    private static void Finish(IXLWorksheet sheet, int columns)
    {
        sheet.SheetView.FreezeRows(6);
        sheet.Columns(1, columns).AdjustToContents(8, 45);
        sheet.RangeUsed()?.Style.Border.SetBottomBorder(XLBorderStyleValues.Hair);
    }

    private static void PdfHeader(TableDescriptor table, IReadOnlyList<string> labels)
    {
        foreach (var label in labels)
            table.Cell().Background(Colors.Green.Darken2).Padding(3).Text(label).FontColor(Colors.White).Bold();
    }

    private static void Cell(TableDescriptor table, string value, bool right = false)
    {
        var cell = table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        if (right) cell.AlignRight().Text(value);
        else cell.Text(value);
    }

    private static string NormalizeMode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "timeline" => "timeline",
        "consolidated" => "consolidated",
        "both" or "" => "both",
        _ => throw new ArgumentException("Mode timeline, consolidated yoki both bo'lishi kerak.", nameof(value))
    };

    private static string Range(TradeCaseStatementDto value) =>
        $"{value.From?.ToLocalTime().ToString("dd.MM.yyyy") ?? "boshlanishidan"} — {value.To?.ToLocalTime().ToString("dd.MM.yyyy") ?? "hozirgacha"}";
}
