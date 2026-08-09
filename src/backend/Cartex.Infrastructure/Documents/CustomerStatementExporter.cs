using Cartex.Application.Common.Interfaces;
using Cartex.Shared.Models.Customers;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cartex.Infrastructure.Documents;

public sealed class CustomerStatementExporter : ICustomerStatementExporter
{
    public CustomerStatementExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public GeneratedDocument Export(CustomerStatementDto statement, string format, string mode)
    {
        format = format.Trim().ToLowerInvariant();
        mode = NormalizeMode(mode);
        var safe = string.Concat(statement.CustomerName.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return format switch
        {
            "xlsx" => new GeneratedDocument(Excel(statement, mode),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{safe}-statement.xlsx"),
            "pdf" => new GeneratedDocument(Pdf(statement, mode), "application/pdf",
                $"{safe}-statement.pdf"),
            _ => throw new ArgumentException("Format faqat pdf yoki xlsx bo'lishi mumkin.", nameof(format))
        };
    }

    private static byte[] Excel(CustomerStatementDto value, string mode)
    {
        using var workbook = new XLWorkbook();
        if (mode is "timeline" or "both")
        {
            var sheet = workbook.AddWorksheet("Harakatlar");
            Header(sheet, value, "Harakatlar");
            var headers = new[] { "Sana", "Hujjat", "Turi", "Izoh", "Debet", "Kredit", "Qoldiq", "Valyuta" };
            WriteHeaders(sheet, 7, headers);
            var row = 8;
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
            Finish(sheet, 8, 7);
        }

        if (mode is "consolidated" or "both")
        {
            var sheet = workbook.AddWorksheet("Yakuniy hisob");
            Header(sheet, value, "Yakuniy hisob");
            var headers = new[] { "Mahsulot", "Birlik", "Sotildi", "Savdo qaytdi", "Sof savdo", "Saqlovga berildi", "Saqlovdan qaytdi", "Hisoblandi", "Saqlov qoldig'i", $"Summa ({value.BaseCurrency})" };
            WriteHeaders(sheet, 7, headers);
            var row = 8;
            foreach (var item in value.Products)
            {
                sheet.Cell(row, 1).Value = item.ProductName;
                sheet.Cell(row, 2).Value = item.UnitName;
                sheet.Cell(row, 3).Value = item.Sold;
                sheet.Cell(row, 4).Value = item.SaleReturned;
                sheet.Cell(row, 5).Value = item.NetSold;
                sheet.Cell(row, 6).Value = item.CustodyIssued;
                sheet.Cell(row, 7).Value = item.CustodyReturned;
                sheet.Cell(row, 8).Value = item.CustodySettled;
                sheet.Cell(row, 9).Value = item.CustodyOutstanding;
                sheet.Cell(row, 10).Value = item.ChargedBaseAmount;
                row++;
            }
            Finish(sheet, 10, 7);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] Pdf(CustomerStatementDto value, string mode) => Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(26);
            page.DefaultTextStyle(x => x.FontSize(8.5f));
            page.Header().Column(column =>
            {
                column.Item().Text(value.CustomerName).FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                column.Item().Text($"Davr: {Range(value)} · Asosiy valyuta: {value.BaseCurrency}")
                    .FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(value.TradeCaseNumber))
                    column.Item().Text($"Loyiha: {value.TradeCaseNumber}");
                column.Item().Text("Qoldiq: " + string.Join(" · ", value.Balances.Select(x =>
                    $"{x.ClosingBalance:N2} {x.Currency}"))).Bold();
            });
            page.Content().PaddingVertical(14).Column(column =>
            {
                if (mode is "timeline" or "both")
                {
                    column.Item().Text("Harakatlar").FontSize(12).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(70); c.ConstantColumn(90); c.ConstantColumn(75);
                            c.RelativeColumn(); c.ConstantColumn(62); c.ConstantColumn(62);
                            c.ConstantColumn(68); c.ConstantColumn(38);
                        });
                        PdfHeader(table, ["Sana", "Hujjat", "Turi", "Izoh", "Debet", "Kredit", "Qoldiq", "Val."]);
                        foreach (var row in value.Timeline)
                        {
                            Cell(table, row.OccurredAt.ToLocalTime().ToString("dd.MM.yy HH:mm"));
                            Cell(table, row.DocumentNumber);
                            Cell(table, row.Type);
                            Cell(table, row.Summary);
                            Cell(table, row.Debit == 0 ? "" : row.Debit.ToString("N2"), true);
                            Cell(table, row.Credit == 0 ? "" : row.Credit.ToString("N2"), true);
                            Cell(table, row.RunningBalance.ToString("N2"), true);
                            Cell(table, row.Currency);
                        }
                    });
                }

                if (mode == "both") column.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                if (mode is "consolidated" or "both")
                {
                    column.Item().Text("Yakuniy hisob").FontSize(12).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.ConstantColumn(42); c.ConstantColumn(48);
                            c.ConstantColumn(48); c.ConstantColumn(48); c.ConstantColumn(52);
                            c.ConstantColumn(52); c.ConstantColumn(52); c.ConstantColumn(52); c.ConstantColumn(70);
                        });
                        PdfHeader(table, ["Mahsulot", "Birlik", "Sotildi", "Qaytdi", "Sof", "Berildi", "Qaytdi", "Hisob", "Saqlov", "Summa"]);
                        foreach (var row in value.Products)
                        {
                            Cell(table, row.ProductName); Cell(table, row.UnitName);
                            Cell(table, row.Sold.ToString("0.###"), true);
                            Cell(table, row.SaleReturned.ToString("0.###"), true);
                            Cell(table, row.NetSold.ToString("0.###"), true);
                            Cell(table, row.CustodyIssued.ToString("0.###"), true);
                            Cell(table, row.CustodyReturned.ToString("0.###"), true);
                            Cell(table, row.CustodySettled.ToString("0.###"), true);
                            Cell(table, row.CustodyOutstanding.ToString("0.###"), true);
                            Cell(table, row.ChargedBaseAmount.ToString("N2"), true);
                        }
                    });
                }
            });
            page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium)).Text(text =>
            {
                text.Span("Cartex · "); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
            });
        });
    }).GeneratePdf();

    private static void Header(IXLWorksheet sheet, CustomerStatementDto value, string title)
    {
        sheet.Cell(1, 1).Value = value.CustomerName;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Davr: {Range(value)}";
        sheet.Cell(3, 1).Value = $"Asosiy valyuta: {value.BaseCurrency}";
        sheet.Cell(4, 1).Value = string.IsNullOrWhiteSpace(value.TradeCaseNumber) ? "Barcha hujjatlar" : $"Loyiha: {value.TradeCaseNumber}";
        sheet.Cell(5, 1).Value = "Qoldiq: " + string.Join(" · ", value.Balances.Select(x => $"{x.ClosingBalance:N2} {x.Currency}"));
        sheet.Cell(6, 1).Value = title;
        sheet.Cell(6, 1).Style.Font.Bold = true;
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

    private static void Finish(IXLWorksheet sheet, int columns, int frozenRows)
    {
        sheet.SheetView.FreezeRows(frozenRows);
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
        if (right) cell.AlignRight().Text(value); else cell.Text(value);
    }

    private static string NormalizeMode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "timeline" => "timeline", "consolidated" => "consolidated", "both" or "" => "both",
        _ => throw new ArgumentException("Mode timeline, consolidated yoki both bo'lishi kerak.", nameof(value))
    };

    private static string Range(CustomerStatementDto value) =>
        $"{value.From?.ToLocalTime().ToString("dd.MM.yyyy") ?? "boshlanishidan"} — {value.To?.ToLocalTime().ToString("dd.MM.yyyy") ?? "hozirgacha"}";
}
