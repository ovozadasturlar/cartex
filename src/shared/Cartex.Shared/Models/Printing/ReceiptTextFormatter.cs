using System.Text;
using Cartex.Shared.Localization;
using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Printing;

public enum ReceiptTextStyle
{
    Normal,
    Title,
    Strong,
    Separator,
    StrongSeparator,
    Total
}

public sealed record ReceiptTextLine(
    string Text,
    ReceiptTextStyle Style = ReceiptTextStyle.Normal,
    bool Centered = false);

public sealed record ReceiptTextOptions(
    string? HeaderText,
    string? FooterText,
    int Width,
    bool ShowBusinessName = true,
    bool ShowBranchName = true,
    bool ShowAddress = true,
    bool ShowPhone = true,
    bool ShowCashier = true,
    bool ShowCustomer = true,
    bool ShowReceiptNumber = true,
    bool ShowPaymentDetails = true,
    bool ShowQrCode = true,
    bool ShowElectronicLink = true,
    string? PublicReceiptBaseUrl = null,
    bool ShowLogo = true,
    bool ShowCustomerPhone = true,
    bool ShowCustomerEmail = false,
    string Template = "auto");

public sealed record ReceiptTextDocument(
    int Width,
    IReadOnlyList<ReceiptTextLine> BeforeQr,
    IReadOnlyList<ReceiptTextLine> AfterQr,
    string? QrContent)
{
    public string BeforeQrText => Render(BeforeQr);
    public string AfterQrText => Render(AfterQr);
    public string Text => BeforeQrText + AfterQrText;

    private string Render(IReadOnlyList<ReceiptTextLine> lines)
    {
        if (lines.Count == 0) return string.Empty;
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            var text = line.Centered ? Center(line.Text, Width) : line.Text;
            builder.AppendLine(text);
        }
        return builder.ToString();
    }

    private static string Center(string value, int width)
    {
        if (value.Length >= width) return value;
        return new string(' ', (width - value.Length) / 2) + value;
    }

    public static ReceiptTextDocument Plain(string text, int width)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(value => new ReceiptTextLine(value))
            .ToArray();
        return new ReceiptTextDocument(width, lines, [], null);
    }
}

public static class ReceiptTextFormatter
{
    public static ReceiptTextDocument Format(ReceiptDto receipt, ReceiptTextOptions options)
    {
        var width = ReceiptPaper.Sanitize(options.Width);
        var template = ResolveTemplate(options.Template, width);
        var beforeQr = new List<ReceiptTextLine>();
        var afterQr = new List<ReceiptTextLine>();
        string T(string key) => ReceiptTexts.Get(key, receipt.Language);

        if (options.ShowBusinessName)
            AddWrapped(beforeQr, receipt.BusinessName, width, ReceiptTextStyle.Title, true);
        if (options.ShowBranchName && !string.IsNullOrWhiteSpace(receipt.BranchName))
            AddWrapped(beforeQr, receipt.BranchName, width, ReceiptTextStyle.Strong, true);
        if (options.ShowAddress && !string.IsNullOrWhiteSpace(receipt.BranchAddress))
            AddWrapped(beforeQr, receipt.BranchAddress, width, centered: true);
        var phone = string.IsNullOrWhiteSpace(receipt.BranchPhone) ? receipt.BusinessPhone : receipt.BranchPhone;
        if (options.ShowPhone && !string.IsNullOrWhiteSpace(phone))
            AddWrapped(beforeQr, phone, width, centered: true);
        if (!string.IsNullOrWhiteSpace(options.HeaderText))
            AddWrapped(beforeQr, options.HeaderText, width, centered: true);

        AddBlank(beforeQr);
        var number = options.ShowReceiptNumber ? $"{T("receipt_no")} {receipt.SaleId}" : string.Empty;
        beforeQr.Add(new ReceiptTextLine(Row(number, receipt.SaleDate.ToString("dd.MM.yyyy HH:mm"), width), ReceiptTextStyle.Strong));
        if (options.ShowCashier && !string.IsNullOrWhiteSpace(receipt.UserName))
            AddWrapped(beforeQr, $"{T("cashier")}: {receipt.UserName}", width);
        if (options.ShowCustomer && !string.IsNullOrWhiteSpace(receipt.CustomerName))
            AddWrapped(beforeQr, $"{T("customer")}: {receipt.CustomerName}", width);
        if (options.ShowCustomerPhone && !string.IsNullOrWhiteSpace(receipt.CustomerPhone))
            AddWrapped(beforeQr, $"Tel: {receipt.CustomerPhone}", width);
        if (options.ShowCustomerEmail && !string.IsNullOrWhiteSpace(receipt.CustomerEmail))
            AddWrapped(beforeQr, $"Email: {receipt.CustomerEmail}", width);

        AddBlank(beforeQr);
        if (template == "table")
            AddTable(beforeQr, receipt, width, T);
        else if (template == "compact")
            AddCompact(beforeQr, receipt, width, T);
        else
            AddLines(beforeQr, receipt, width, T);

        AddBlank(beforeQr);
        // Chegirma jamini kamaytiradi, shuning uchun u JAMI dan oldin turadi; to'lov esa
        // mijoz bergan pul — u JAMI dan keyin ko'rinadi.
        if (receipt.DiscountAmount > 0)
            beforeQr.Add(new ReceiptTextLine(Row(T("discount"), $"-{receipt.DiscountAmount:N0}", width)));
        beforeQr.Add(new ReceiptTextLine(new string('=', width), ReceiptTextStyle.StrongSeparator));
        beforeQr.Add(new ReceiptTextLine(Row(T("total"), $"{receipt.TotalAmount:N0}", width), ReceiptTextStyle.Total));
        if (options.ShowPaymentDetails)
            AddPayments(beforeQr, receipt, width, T);
        beforeQr.Add(new ReceiptTextLine(T(receipt.DebtAmount > 0 ? "unpaid" : "paid"), ReceiptTextStyle.Strong, true));
        AddBlank(beforeQr);
        AddWrapped(
            beforeQr,
            string.IsNullOrWhiteSpace(options.FooterText) ? T("thanks") : options.FooterText,
            width,
            centered: true);

        var link = !string.IsNullOrWhiteSpace(options.PublicReceiptBaseUrl)
            ? $"{options.PublicReceiptBaseUrl.TrimEnd('/')}/r/{receipt.ReceiptToken}"
            : null;
        var qrContent = options.ShowQrCode ? link : null;
        if (qrContent is not null)
        {
            afterQr.Add(new ReceiptTextLine(T("qr_caption"), Centered: true));
            if (options.ShowElectronicLink)
                AddWrapped(afterQr, link!, width, centered: true);
        }
        else if (options.ShowElectronicLink && link is not null)
        {
            AddWrapped(beforeQr, link, width, centered: true);
        }
        AddBlank(qrContent is null ? beforeQr : afterQr);

        return new ReceiptTextDocument(width, beforeQr, afterQr, qrContent);
    }

    public static string ResolveTemplate(string? template, int width) => template switch
    {
        "compact" => "compact",
        "table" => "table",
        "lines" => "lines",
        _ => "lines"
    };

    /// Nom butun kenglikni oladi va hech qachon qisqartirilmaydi; raqamlar keyingi
    /// qatorda tekislangan ustunlarda turadi — shu sabab shrift kattaroq bo'la oladi.
    private static void AddLines(
        List<ReceiptTextLine> lines,
        ReceiptDto receipt,
        int width,
        Func<string, string> text)
    {
        var tight = width <= 28;
        var narrow = width <= 36;
        var indent = tight ? 1 : narrow ? 2 : 3;
        var priceWidth = tight ? 7 : narrow ? 8 : 9;
        var amountWidth = tight ? 8 : narrow ? 9 : 10;
        var measureWidth = Math.Max(4, width - indent - priceWidth - amountWidth - 2);

        for (var index = 0; index < receipt.Items.Count; index++)
        {
            var item = receipt.Items[index];
            var prefix = $"{index + 1} ";
            foreach (var part in Wrap(item.ProductName, width - prefix.Length))
            {
                lines.Add(new ReceiptTextLine(prefix + part, ReceiptTextStyle.Strong));
                prefix = new string(' ', prefix.Length);
            }
            lines.Add(new ReceiptTextLine(DetailRow(
                $"{item.Quantity:0.###} {item.UnitName}",
                $"{item.UnitPrice:N0}",
                $"{item.LineTotal:N0}",
                indent, measureWidth, priceWidth, amountWidth)));
            if (item.DiscountAmount > 0)
                lines.Add(new ReceiptTextLine(DetailRow(
                    text("discount"),
                    $"{item.DiscountAmount:N0}",
                    $"{item.NetTotal:N0}",
                    indent, measureWidth, priceWidth, amountWidth)));
        }
    }

    private static string DetailRow(
        string measure,
        string price,
        string amount,
        int indent,
        int measureWidth,
        int priceWidth,
        int amountWidth) =>
        new string(' ', indent)
        + $"{Fit(measure, measureWidth).PadRight(measureWidth)} "
        + $"{Fit(price, priceWidth).PadLeft(priceWidth)} "
        + $"{Fit(amount, amountWidth).PadLeft(amountWidth)}";

    private static void AddCompact(
        List<ReceiptTextLine> lines,
        ReceiptDto receipt,
        int width,
        Func<string, string> text)
    {
        foreach (var item in receipt.Items)
        {
            AddWrapped(lines, item.ProductName, width, ReceiptTextStyle.Strong);
            lines.Add(new ReceiptTextLine(Row(
                $"  {item.Quantity:0.###} {item.UnitName} x {item.UnitPrice:N0}",
                $"{item.LineTotal:N0}",
                width)));
            if (item.DiscountAmount > 0)
                lines.Add(new ReceiptTextLine(Row(
                    $"  {text("discount")} -{item.DiscountAmount:N0}",
                    $"{item.NetTotal:N0}",
                    width)));
        }
    }

    private static void AddTable(
        List<ReceiptTextLine> lines,
        ReceiptDto receipt,
        int width,
        Func<string, string> text)
    {
        const int numberWidth = 2;
        const int unitWidth = 5;
        const int quantityWidth = 6;
        const int priceWidth = 10;
        const int amountWidth = 10;
        const int gaps = 5;
        var nameWidth = Math.Max(6, width - numberWidth - unitWidth - quantityWidth - priceWidth - amountWidth - gaps);
        lines.Add(new ReceiptTextLine(TableRow(
            "№",
            text("item"),
            text("unit"),
            text("qty"),
            text("price"),
            text("amount"),
            numberWidth,
            nameWidth,
            unitWidth,
            quantityWidth,
            priceWidth,
            amountWidth), ReceiptTextStyle.Strong));
        lines.Add(new ReceiptTextLine(new string('-', width), ReceiptTextStyle.Separator));

        for (var index = 0; index < receipt.Items.Count; index++)
        {
            var item = receipt.Items[index];
            // Uzun nom tor ustunga siqilib bir necha qatorga bo'linsa o'qib bo'lmaydi,
            // shuning uchun u raqamlar qatoridan oldin butun kenglikda yoziladi.
            var number = (index + 1).ToString();
            if (item.ProductName.Length > nameWidth)
            {
                var prefix = $"{Fit(number, numberWidth).PadRight(numberWidth)} ";
                foreach (var part in Wrap(item.ProductName, width - prefix.Length))
                {
                    lines.Add(new ReceiptTextLine(prefix + part));
                    prefix = new string(' ', prefix.Length);
                }
                number = string.Empty;
            }
            lines.Add(new ReceiptTextLine(TableRow(
                number,
                item.ProductName.Length > nameWidth ? string.Empty : item.ProductName,
                item.UnitName,
                $"{item.Quantity:0.###}",
                $"{item.UnitPrice:N0}",
                $"{item.LineTotal:N0}",
                numberWidth,
                nameWidth,
                unitWidth,
                quantityWidth,
                priceWidth,
                amountWidth)));
            if (item.DiscountAmount > 0)
                lines.Add(new ReceiptTextLine(Row(
                    $"  {text("discount")} -{item.DiscountAmount:N0}",
                    $"{item.NetTotal:N0}",
                    width)));
        }
    }

    private static void AddPayments(
        List<ReceiptTextLine> lines,
        ReceiptDto receipt,
        int width,
        Func<string, string> text)
    {
        if (receipt.Payments.Count > 0)
            foreach (var payment in receipt.Payments)
                lines.Add(new ReceiptTextLine(Row(
                    ReceiptTexts.PaymentLabel(payment.Method, receipt.Language),
                    payment.IsForeign
                        ? $"{payment.Amount:N2} {payment.Currency} ~ {payment.AmountBase:N0}"
                        : $"{payment.Amount:N0}",
                    width)));
        else
        {
            if (receipt.PaidCash > 0) lines.Add(new ReceiptTextLine(Row(text("cash"), $"{receipt.PaidCash:N0}", width)));
            if (receipt.PaidCard > 0) lines.Add(new ReceiptTextLine(Row(text("card"), $"{receipt.PaidCard:N0}", width)));
            if (receipt.PaidBonus > 0) lines.Add(new ReceiptTextLine(Row(text("bonus"), $"{receipt.PaidBonus:N0}", width)));
        }
        if (receipt.PaidAdvance > 0) lines.Add(new ReceiptTextLine(Row(text("advance"), $"{receipt.PaidAdvance:N0}", width)));
        if (receipt.ChangeAmount > 0) lines.Add(new ReceiptTextLine(Row(text("change"), $"{receipt.ChangeAmount:N0}", width)));
        if (receipt.CreditAmount > 0) lines.Add(new ReceiptTextLine(Row(text("credit"), $"{receipt.CreditAmount:N0}", width)));
        if (receipt.DebtAmount > 0) lines.Add(new ReceiptTextLine(Row(text("debt"), $"{receipt.DebtAmount:N0}", width)));
        if (receipt.CashbackEarned > 0) lines.Add(new ReceiptTextLine(Row(text("cashback"), $"{receipt.CashbackEarned:N0}", width)));
    }

    private static string TableRow(
        string number,
        string name,
        string unit,
        string quantity,
        string price,
        string amount,
        int numberWidth,
        int nameWidth,
        int unitWidth,
        int quantityWidth,
        int priceWidth,
        int amountWidth) =>
        $"{Fit(number, numberWidth).PadRight(numberWidth)} {Fit(name, nameWidth).PadRight(nameWidth)} "
        + $"{Fit(unit, unitWidth).PadRight(unitWidth)} {Fit(quantity, quantityWidth).PadLeft(quantityWidth)} "
        + $"{Fit(price, priceWidth).PadLeft(priceWidth)} {Fit(amount, amountWidth).PadLeft(amountWidth)}";

    private static string Fit(string value, int width) => value.Length <= width ? value : value[..width];

    private static string Row(string left, string right, int width)
    {
        if (string.IsNullOrEmpty(left)) return right.Length >= width ? right[^width..] : right.PadLeft(width);
        if (right.Length >= width) return right[^width..];
        var leftWidth = width - right.Length - 1;
        var fittedLeft = left.Length <= leftWidth ? left : left[..leftWidth];
        return fittedLeft + new string(' ', width - fittedLeft.Length - right.Length) + right;
    }

    private static void AddWrapped(
        List<ReceiptTextLine> lines,
        string? value,
        int width,
        ReceiptTextStyle style = ReceiptTextStyle.Normal,
        bool centered = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (var line in Wrap(value, width))
            lines.Add(new ReceiptTextLine(line, style, centered));
    }

    private static IEnumerable<string> Wrap(string value, int width)
    {
        foreach (var paragraph in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var remaining = paragraph.Trim();
            if (remaining.Length == 0)
            {
                yield return string.Empty;
                continue;
            }
            while (remaining.Length > width)
            {
                var split = remaining.LastIndexOf(" ", width - 1, width, StringComparison.Ordinal);
                if (split <= 0) split = width;
                yield return remaining[..split].TrimEnd();
                remaining = remaining[split..].TrimStart();
            }
            yield return remaining;
        }
    }

    private static void AddBlank(List<ReceiptTextLine> lines)
    {
        if (lines.Count == 0 || lines[^1].Text.Length != 0)
            lines.Add(new ReceiptTextLine(string.Empty));
    }
}
