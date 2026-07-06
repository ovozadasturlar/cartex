using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sales.Queries;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cartex.Infrastructure.Notifications;

public sealed class ReceiptPdfRenderer : IReceiptPdfRenderer
{
    static ReceiptPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReceiptDto receipt)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(5, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(9));
                page.Content().Column(col =>
                {
                    col.Spacing(2);
                    col.Item().AlignCenter().Text(receipt.BusinessName).FontSize(13).Bold();
                    col.Item().AlignCenter().Text(receipt.BranchName).FontSize(9);
                    if (!string.IsNullOrEmpty(receipt.BranchAddress))
                        col.Item().AlignCenter().Text(receipt.BranchAddress).FontSize(8);
                    col.Item().AlignCenter().Text(receipt.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm")).FontSize(8);
                    col.Item().LineHorizontal(0.5f);

                    foreach (var item in receipt.Items)
                    {
                        col.Item().Text(item.ProductName).SemiBold();
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{item.Quantity:0.###} {item.UnitName} x {item.UnitPrice:N0}");
                            row.ConstantItem(70).AlignRight().Text($"{item.LineTotal:N0}");
                        });
                    }

                    col.Item().LineHorizontal(0.5f);
                    if (receipt.DiscountAmount > 0)
                        Line(col, "Chegirma", receipt.DiscountAmount);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("JAMI").FontSize(12).Bold();
                        row.ConstantItem(90).AlignRight().Text($"{receipt.TotalAmount:N0}").FontSize(12).Bold();
                    });
                    if (receipt.PaidCash > 0) Line(col, "Naqd", receipt.PaidCash);
                    if (receipt.PaidCard > 0) Line(col, "Karta", receipt.PaidCard);
                    if (receipt.PaidBonus > 0) Line(col, "Bonus", receipt.PaidBonus);
                    if (receipt.ChangeAmount > 0) Line(col, "Qaytim", receipt.ChangeAmount);
                    if (receipt.DebtAmount > 0) Line(col, "Qarz", receipt.DebtAmount);
                    if (receipt.CashbackEarned > 0) Line(col, "Cashback", receipt.CashbackEarned);

                    foreach (var payment in receipt.Payments)
                        Line(col, $"{payment.Method} {payment.Currency}", payment.Amount);

                    col.Item().LineHorizontal(0.5f);
                    col.Item().AlignCenter().Text(receipt.UserName).FontSize(8);
                    col.Item().AlignCenter().Text("Xaridingiz uchun rahmat!").FontSize(8);
                });
            });
        }).GeneratePdf();

        static void Line(ColumnDescriptor col, string label, decimal value) =>
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label);
                row.ConstantItem(90).AlignRight().Text($"{value:N0}");
            });
    }
}
