using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Shared.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Cartex.Shared.Models.Sales;

namespace Cartex.Infrastructure.Notifications;

public sealed class ReceiptPdfRenderer : IReceiptPdfRenderer
{
    static ReceiptPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(ReceiptDto receipt, ReceiptSettings? settings = null)
    {
        var lang = receipt.Language;
        string T(string key) => ReceiptTexts.Get(key, lang);
        var link = ReceiptLink(settings, receipt);
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
                    if (settings?.ShowLogo != false && receipt.LogoBytes is { Length: > 0 })
                        col.Item().PaddingBottom(2).AlignCenter().MaxWidth(80).MaxHeight(55).Image(receipt.LogoBytes).FitArea();
                    if (settings?.ShowBusinessName != false)
                        col.Item().AlignCenter().Text(receipt.BusinessName).FontSize(13).Bold();
                    if (settings?.ShowBranchName != false)
                        col.Item().AlignCenter().Text(receipt.BranchName).FontSize(9);
                    if (settings?.ShowAddress != false && !string.IsNullOrEmpty(receipt.BranchAddress))
                        col.Item().AlignCenter().Text(receipt.BranchAddress).FontSize(8);
                    if (settings?.ShowPhone != false && !string.IsNullOrEmpty(receipt.BranchPhone))
                        col.Item().AlignCenter().Text(receipt.BranchPhone).FontSize(8);
                    if (!string.IsNullOrWhiteSpace(settings?.HeaderText))
                        col.Item().AlignCenter().Text(settings.HeaderText).FontSize(8);
                    col.Item().AlignCenter().Text(receipt.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm")).FontSize(8);
                    if (settings?.ShowReceiptNumber != false)
                        col.Item().AlignCenter().Text($"{T("receipt_no")} {receipt.SaleId}").FontSize(8);
                    if (settings?.ShowCashier != false && !string.IsNullOrWhiteSpace(receipt.UserName))
                        col.Item().AlignCenter().Text($"{T("cashier")}: {receipt.UserName}").FontSize(8);
                    if (settings?.ShowCustomer != false && !string.IsNullOrWhiteSpace(receipt.CustomerName))
                        col.Item().AlignCenter().Text($"{T("customer")}: {receipt.CustomerName}").FontSize(8);
                    if (settings?.ShowCustomerPhone != false && !string.IsNullOrWhiteSpace(receipt.CustomerPhone))
                        col.Item().AlignCenter().Text(receipt.CustomerPhone).FontSize(8);
                    if (settings?.ShowCustomerEmail == true && !string.IsNullOrWhiteSpace(receipt.CustomerEmail))
                        col.Item().AlignCenter().Text(receipt.CustomerEmail).FontSize(8);
                    col.Item().LineHorizontal(0.5f);

                    foreach (var item in receipt.Items)
                    {
                        col.Item().Text(item.ProductName).SemiBold();
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"{item.Quantity:0.###} {item.UnitName} x {item.UnitPrice:N0}");
                            row.ConstantItem(70).AlignRight().Text($"{item.LineTotal:N0}");
                        });
                        if (item.DiscountAmount > 0)
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{T("discount")} −{item.DiscountAmount:N0}");
                                row.ConstantItem(70).AlignRight().Text($"{item.LineTotal - item.DiscountAmount:N0}");
                            });
                    }

                    col.Item().LineHorizontal(0.5f);
                    if (receipt.DiscountAmount > 0)
                        Line(col, T("discount"), $"{receipt.DiscountAmount:N0}");
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(T("total")).FontSize(12).Bold();
                        row.ConstantItem(90).AlignRight().Text($"{receipt.TotalAmount:N0}").FontSize(12).Bold();
                    });
                    if (settings?.ShowPaymentDetails != false)
                    {
                        if (receipt.Payments.Count > 0)
                            foreach (var payment in receipt.Payments)
                                Line(col, ReceiptTexts.PaymentLabel(payment.Method, lang),
                                    payment.IsForeign ? $"{payment.Amount:N2} {payment.Currency} ≈ {payment.AmountBase:N0}" : $"{payment.Amount:N0}");
                        else
                        {
                            if (receipt.PaidCash > 0) Line(col, T("cash"), $"{receipt.PaidCash:N0}");
                            if (receipt.PaidCard > 0) Line(col, T("card"), $"{receipt.PaidCard:N0}");
                            if (receipt.PaidBonus > 0) Line(col, T("bonus"), $"{receipt.PaidBonus:N0}");
                        }
                        if (receipt.PaidAdvance > 0) Line(col, T("advance"), $"{receipt.PaidAdvance:N0}");
                        if (receipt.ChangeAmount > 0) Line(col, T("change"), $"{receipt.ChangeAmount:N0}");
                        if (receipt.CreditAmount > 0) Line(col, T("credit"), $"{receipt.CreditAmount:N0}");
                        if (receipt.DebtAmount > 0) Line(col, T("debt"), $"{receipt.DebtAmount:N0}");
                        if (receipt.CashbackEarned > 0) Line(col, "Cashback", $"{receipt.CashbackEarned:N0}");
                    }

                    col.Item().LineHorizontal(0.5f);
                    col.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(settings?.FooterText) ? T("thanks") : settings.FooterText).FontSize(8);
                    if (settings?.ShowQrCode != false && link is not null)
                        col.Item().PaddingTop(3).AlignCenter().Width(80).Height(80).Image(ReceiptQrCode.RenderPng(link));
                    if (settings?.ShowElectronicLink != false && link is not null)
                        col.Item().AlignCenter().Text(link).FontSize(6);
                });
            });
        }).GeneratePdf();

        static void Line(ColumnDescriptor col, string label, string value) =>
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label);
                row.ConstantItem(90).AlignRight().Text(value);
            });
    }

    public byte[] RenderDocument(ReceiptDto receipt, ReceiptSettings? settings = null, bool a4 = false) =>
        BuildDocument(receipt, settings, a4, landscape: false).GeneratePdf();

    public IReadOnlyList<byte[]> RenderDocumentImages(ReceiptDto receipt, ReceiptSettings? settings = null, bool a4 = false, bool landscape = false) =>
        BuildDocument(receipt, settings, a4, landscape)
            .GenerateImages(new ImageGenerationSettings { RasterDpi = 200 })
            .ToList();

    private static IDocument BuildDocument(ReceiptDto receipt, ReceiptSettings? settings, bool a4, bool landscape)
    {
        var lang = receipt.Language;
        string T(string key) => ReceiptTexts.Get(key, lang);
        const string ink = "#1E293B";
        const string muted = "#64748B";
        const string faint = "#F1F5F9";
        const string line = "#CBD5E1";
        const string danger = "#DC2626";
        const string success = "#16A34A";
        var subtotal = receipt.TotalAmount + receipt.DiscountAmount;
        var link = ReceiptLink(settings, receipt);
        var size = a4 ? PageSizes.A4 : PageSizes.A5;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? size.Landscape() : size);
                page.Margin(a4 ? 20 : 14, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(a4 ? 10 : 9).FontColor(ink));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        if (settings?.ShowLogo != false && receipt.LogoBytes is { Length: > 0 })
                            row.ConstantItem(a4 ? 72 : 58).PaddingRight(10).AlignMiddle().Image(receipt.LogoBytes).FitArea();
                        row.RelativeItem().Column(left =>
                        {
                            if (settings?.ShowBusinessName != false)
                                left.Item().Text(receipt.BusinessName).FontSize(a4 ? 20 : 16).Bold();
                            if (settings?.ShowBranchName != false)
                                left.Item().Text(receipt.BranchName).FontColor(muted);
                            if (settings?.ShowAddress != false && !string.IsNullOrWhiteSpace(receipt.BranchAddress))
                                left.Item().Text(receipt.BranchAddress).FontSize(8).FontColor(muted);
                            if (settings?.ShowPhone != false && !string.IsNullOrWhiteSpace(receipt.BranchPhone))
                                left.Item().Text(receipt.BranchPhone).FontSize(8).FontColor(muted);
                            else if (settings?.ShowPhone != false && !string.IsNullOrWhiteSpace(receipt.BusinessPhone))
                                left.Item().Text(receipt.BusinessPhone).FontSize(8).FontColor(muted);
                            if (!string.IsNullOrWhiteSpace(receipt.BusinessTelegram))
                                left.Item().Text(receipt.BusinessTelegram).FontSize(8).FontColor(muted);
                            if (!string.IsNullOrWhiteSpace(receipt.BusinessWebsite))
                                left.Item().Text(receipt.BusinessWebsite).FontSize(8).FontColor(muted);
                        });
                        row.ConstantItem(a4 ? 180 : 150).Column(right =>
                        {
                            if (settings?.ShowReceiptNumber != false)
                                right.Item().AlignRight().Text($"{T("receipt_no")} {receipt.SaleId}").FontSize(a4 ? 14 : 12).Bold();
                            right.Item().AlignRight().Text(receipt.SaleDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm")).FontColor(muted);
                            if (settings?.ShowCashier != false)
                                right.Item().AlignRight().Text($"{T("cashier")}: {receipt.UserName}").FontSize(8).FontColor(muted);
                            if (settings?.ShowCustomer != false && !string.IsNullOrWhiteSpace(receipt.CustomerName))
                                right.Item().AlignRight().Text($"{T("customer")}: {receipt.CustomerName}").FontSize(8).FontColor(muted);
                            if (settings?.ShowCustomerPhone != false && !string.IsNullOrWhiteSpace(receipt.CustomerPhone))
                                right.Item().AlignRight().Text(receipt.CustomerPhone).FontSize(8).FontColor(muted);
                            if (settings?.ShowCustomerEmail == true && !string.IsNullOrWhiteSpace(receipt.CustomerEmail))
                                right.Item().AlignRight().Text(receipt.CustomerEmail).FontSize(8).FontColor(muted);
                        });
                    });
                    if (!string.IsNullOrWhiteSpace(settings?.HeaderText))
                        col.Item().PaddingTop(4).Text(settings.HeaderText).FontSize(8).FontColor(muted);
                    col.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(ink);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        var anyDiscount = receipt.Items.Any(x => x.DiscountAmount > 0);
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(a4 ? 28 : 22);
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            if (anyDiscount) columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(ink).PaddingVertical(4);
                            header.Cell().Element(Cell).Text("№").SemiBold();
                            header.Cell().Element(Cell).Text(T("item")).SemiBold();
                            header.Cell().Element(Cell).AlignRight().Text(T("qty")).SemiBold();
                            header.Cell().Element(Cell).AlignRight().Text(T("price")).SemiBold();
                            if (anyDiscount) header.Cell().Element(Cell).AlignRight().Text(T("discount")).SemiBold();
                            header.Cell().Element(Cell).AlignRight().Text(T("amount")).SemiBold();
                        });

                        for (var i = 0; i < receipt.Items.Count; i++)
                        {
                            var item = receipt.Items[i];
                            var background = i % 2 == 1 ? faint : "#FFFFFF";
                            IContainer Cell(IContainer c) => c.Background(background).BorderBottom(0.5f).BorderColor(line).PaddingVertical(3).PaddingHorizontal(1);
                            table.Cell().Element(Cell).Text($"{i + 1}").FontColor(muted);
                            table.Cell().Element(Cell).Text(item.ProductName);
                            table.Cell().Element(Cell).AlignRight().Text($"{item.Quantity:0.###} {item.UnitName}");
                            table.Cell().Element(Cell).AlignRight().Text($"{item.UnitPrice:N0}");
                            if (anyDiscount)
                                table.Cell().Element(Cell).AlignRight().Text(item.DiscountAmount > 0 ? $"−{item.DiscountAmount:N0}" : "");
                            table.Cell().Element(Cell).AlignRight().Text($"{item.LineTotal - item.DiscountAmount:N0}").SemiBold();
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Width(a4 ? 260 : 220).Column(totals =>
                    {
                        void Row(string label, string value, string? color = null, bool bold = false)
                        {
                            totals.Item().Row(row =>
                            {
                                row.RelativeItem().Text(label).FontColor(color ?? muted).SemiBold();
                                var text = row.ConstantItem(110).AlignRight().Text(value);
                                if (bold) text.Bold();
                                if (color is not null) text.FontColor(color);
                            });
                        }

                        if (receipt.DiscountAmount > 0)
                        {
                            Row(T("subtotal"), $"{subtotal:N0}");
                            Row(T("discount"), $"{-receipt.DiscountAmount:N0}");
                        }
                        totals.Item().PaddingVertical(3).LineHorizontal(1).LineColor(ink);
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text(T("total")).FontSize(a4 ? 14 : 12).Bold();
                            row.ConstantItem(110).AlignRight().Text($"{receipt.TotalAmount:N0}").FontSize(a4 ? 14 : 12).Bold();
                        });
                        totals.Item().PaddingTop(4);
                        if (settings?.ShowPaymentDetails != false)
                        {
                            if (receipt.Payments.Count > 0)
                                foreach (var payment in receipt.Payments)
                                    Row(ReceiptTexts.PaymentLabel(payment.Method, lang),
                                        payment.IsForeign ? $"{payment.Amount:N2} {payment.Currency} ≈ {payment.AmountBase:N0}" : $"{payment.Amount:N0}");
                            else
                            {
                                if (receipt.PaidCash > 0) Row(T("cash"), $"{receipt.PaidCash:N0}");
                                if (receipt.PaidCard > 0) Row(T("card"), $"{receipt.PaidCard:N0}");
                                if (receipt.PaidBonus > 0) Row(T("bonus"), $"{receipt.PaidBonus:N0}");
                            }
                            if (receipt.ChangeAmount > 0) Row(T("change"), $"{receipt.ChangeAmount:N0}");
                            if (receipt.CreditAmount > 0) Row(T("credit"), $"{receipt.CreditAmount:N0}");
                            if (receipt.DebtAmount > 0) Row(T("debt"), $"{receipt.DebtAmount:N0}", danger, bold: true);
                            if (receipt.CashbackEarned > 0) Row(T("cashback"), $"{receipt.CashbackEarned:N0}", success);
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight()
                        .Background(receipt.DebtAmount > 0 ? "#FEF2F2" : "#F0FDF4")
                        .Border(1).BorderColor(receipt.DebtAmount > 0 ? danger : success)
                        .PaddingVertical(3).PaddingHorizontal(12)
                        .Text(T(receipt.DebtAmount > 0 ? "unpaid" : "paid"))
                        .FontColor(receipt.DebtAmount > 0 ? danger : success).Bold();
                });

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(0.5f).LineColor(line);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text(string.IsNullOrWhiteSpace(settings?.FooterText) ? T("thanks") : settings.FooterText).FontSize(8).FontColor(muted);
                        if (settings?.ShowReceiptNumber != false)
                            row.ConstantItem(120).AlignRight().Text($"{T("receipt_no")} {receipt.SaleId}").FontSize(8).FontColor(muted);
                    });
                    if (link is not null)
                        col.Item().PaddingTop(4).Row(row =>
                        {
                            if (settings?.ShowElectronicLink != false)
                                row.RelativeItem().AlignMiddle().Text(link).FontSize(7).FontColor(muted);
                            if (settings?.ShowQrCode != false)
                                row.ConstantItem(55).Height(55).Image(ReceiptQrCode.RenderPng(link));
                        });
                });
            });
        });
    }

    private static string? ReceiptLink(ReceiptSettings? settings, ReceiptDto receipt)
    {
        if (string.IsNullOrWhiteSpace(settings?.PublicReceiptBaseUrl))
            return null;

        return $"{settings.PublicReceiptBaseUrl.TrimEnd('/')}/r/{receipt.ReceiptToken}";
    }
}
