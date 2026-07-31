using Cartex.Shared.Models.Settings;
using Cartex.Shared.Models.Shifts;
using SkiaSharp;

namespace Cartex.UI.Services;

public sealed record ZReportDocumentMetadata(
    string BusinessName,
    string? BranchName,
    string? CashierName,
    DateTime PrintedAt);

public static class ZReportDocumentRenderer
{
    private const double Dpi = 150;
    private const double MillimetersPerInch = 25.4;

    public static IReadOnlyList<byte[]> Render(
        ZReportDto report,
        ZReportDocumentMetadata metadata,
        string paperSize,
        string orientation,
        bool supportsColor)
    {
        var dimensions = DocumentPrintLayout.GetPaperDimensions(paperSize, orientation);
        var pixelsPerMm = (float)(Dpi / MillimetersPerInch);
        var width = Math.Max(1, (int)Math.Round(dimensions.WidthMm * pixelsPerMm));
        var height = Math.Max(1, (int)Math.Round(dimensions.HeightMm * pixelsPerMm));

        using var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var regularTypeface = SKTypeface.FromFamilyName("Segoe UI");
        using var boldTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
        var palette = ZReportPalette.Create(supportsColor);
        var margin = (paperSize == "a5" ? 11f : 15f) * pixelsPerMm;
        var contentWidth = width - margin * 2;
        var y = margin;
        var l = LocalizationManager.Instance;

        DrawHeader(
            canvas,
            metadata,
            report,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            contentWidth,
            pixelsPerMm,
            ref y);

        DrawMetadata(
            canvas,
            metadata,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            contentWidth,
            pixelsPerMm,
            ref y);

        DrawSummaryCards(
            canvas,
            report,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            contentWidth,
            pixelsPerMm,
            ref y);

        var footerHeight = 9f * pixelsPerMm;
        var detailsBottom = height - margin - footerHeight;
        DrawDetailCards(
            canvas,
            report,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            y,
            contentWidth,
            Math.Max(35f * pixelsPerMm, detailsBottom - y),
            pixelsPerMm);

        DrawFooter(
            canvas,
            metadata,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            height - margin - footerHeight,
            contentWidth,
            footerHeight,
            pixelsPerMm);

        canvas.Flush();
        var pages = new List<byte[]> { Encode(bitmap) };
        foreach (var currency in report.Currencies)
        {
            pages.Add(RenderCurrencyPage(
                report,
                currency,
                metadata,
                width,
                height,
                margin,
                pixelsPerMm,
                palette,
                l));
        }
        return pages;
    }

    private static void DrawHeader(
        SKCanvas canvas,
        ZReportDocumentMetadata metadata,
        ZReportDto report,
        LocalizationManager l,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var height = 13f * scale;
        DrawRoundRect(canvas, new SKRect(x, y, x + 1.4f * scale, y + height), palette.Accent, palette.Accent, 0, 0.7f * scale);

        var textX = x + 4.5f * scale;
        DrawText(canvas, metadata.BusinessName, textX, y + 5f * scale, 4.2f * scale, palette.Ink, bold);
        if (!string.IsNullOrWhiteSpace(metadata.BranchName))
            DrawText(canvas, metadata.BranchName, textX, y + 9f * scale, 2.4f * scale, palette.Muted, regular);

        DrawText(canvas, l["z_report"], x + width, y + 4.5f * scale, 4f * scale, palette.Accent, bold, SKTextAlign.Right);
        DrawText(canvas, $"{l["shift"]} #{report.ShiftId}", x + width, y + 9f * scale, 2.4f * scale, palette.Muted, regular, SKTextAlign.Right);
        y += height + 4f * scale;
    }

    private static void DrawMetadata(
        SKCanvas canvas,
        ZReportDocumentMetadata metadata,
        LocalizationManager l,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var height = 13f * scale;
        DrawRoundRect(canvas, new SKRect(x, y, x + width, y + height), palette.Faint, palette.Line, 0.35f * scale, 1.2f * scale);
        var padding = 4f * scale;
        DrawText(canvas, l["cashier"], x + padding, y + 4.3f * scale, 2.1f * scale, palette.Muted, regular);
        DrawText(
            canvas,
            string.IsNullOrWhiteSpace(metadata.CashierName) ? "—" : metadata.CashierName,
            x + padding,
            y + 9.1f * scale,
            2.8f * scale,
            palette.Ink,
            bold);
        DrawText(canvas, metadata.PrintedAt.ToString("dd.MM.yyyy"), x + width - padding, y + 4.3f * scale, 2.1f * scale, palette.Muted, regular, SKTextAlign.Right);
        DrawText(canvas, metadata.PrintedAt.ToString("HH:mm"), x + width - padding, y + 9.1f * scale, 2.8f * scale, palette.Ink, bold, SKTextAlign.Right);
        y += height + 4f * scale;
    }

    private static void DrawSummaryCards(
        SKCanvas canvas,
        ZReportDto report,
        LocalizationManager l,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var gap = 2.5f * scale;
        var cardWidth = (width - gap * 3) / 4;
        var cardHeight = 16f * scale;
        var items = new[]
        {
            (l["z_section_sales"], report.CashSales + report.CardSales, false),
            (l["expected_cash"], report.ExpectedCash, false),
            (l["counted_cash"], report.CountedCash, false),
            (l["difference"], report.Difference, true)
        };

        for (var index = 0; index < items.Length; index++)
        {
            var cardX = x + index * (cardWidth + gap);
            var background = items[index].Item3 ? palette.StatusBackground : palette.Faint;
            var border = items[index].Item3 ? palette.Status : palette.Faint;
            DrawRoundRect(canvas, new SKRect(cardX, y, cardX + cardWidth, y + cardHeight), background, border, 0.35f * scale, 1.2f * scale);
            var foreground = items[index].Item3 ? palette.Status : palette.Ink;
            DrawText(canvas, items[index].Item1, cardX + 3f * scale, y + 5f * scale, 2f * scale, palette.Muted, regular);
            DrawText(canvas, FormatAmount(items[index].Item2), cardX + 3f * scale, y + 11.5f * scale, 3.1f * scale, foreground, bold);
        }

        y += cardHeight + 4f * scale;
    }

    private static void DrawDetailCards(
        SKCanvas canvas,
        ZReportDto report,
        LocalizationManager l,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float y,
        float width,
        float height,
        float scale)
    {
        var gap = 4f * scale;
        var cardWidth = (width - gap) / 2;
        DrawRoundRect(canvas, new SKRect(x, y, x + cardWidth, y + height), SKColors.White, palette.Line, 0.35f * scale, 1.2f * scale);
        DrawRoundRect(canvas, new SKRect(x + cardWidth + gap, y, x + width, y + height), SKColors.White, palette.Line, 0.35f * scale, 1.2f * scale);

        var salesRows = new[]
        {
            (l["cash_sales"], FormatAmount(report.CashSales)),
            (l["card_sales"], FormatAmount(report.CardSales)),
            (l["bonus_used"], FormatAmount(report.BonusUsed)),
            (l["debt_issued"], FormatAmount(report.NewDebtIssued)),
            (l["cash_returns"], FormatNegative(report.CashReturns)),
            (l["card_returns"], FormatNegative(report.CardReturns)),
            (l["sales_count"], report.SalesCount.ToString("N0"))
        };
        DrawSection(
            canvas,
            l["z_section_sales"],
            salesRows,
            palette,
            regular,
            bold,
            x,
            y,
            cardWidth,
            scale);

        var cashRows = new List<(string Label, string Value)>
        {
            (l["opening_float"], FormatAmount(report.OpeningFloat)),
            (l["pay_in"], FormatAmount(report.PayIn)),
            (l["pay_out"], FormatNegative(report.PayOut)),
            (l["debt_pay_in"], FormatAmount(report.DebtPayIn)),
            (l["supply_pay_out"], FormatNegative(report.SupplyPayOut))
        };
        DrawSection(
            canvas,
            l["z_section_cash"],
            cashRows,
            palette,
            regular,
            bold,
            x + cardWidth + gap,
            y,
            cardWidth,
            scale);
    }

    private static byte[] RenderCurrencyPage(
        ZReportDto report,
        ZReportCurrencyDto currency,
        ZReportDocumentMetadata metadata,
        int width,
        int height,
        float margin,
        float scale,
        ZReportPalette palette,
        LocalizationManager l)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var regularTypeface = SKTypeface.FromFamilyName("Segoe UI");
        using var boldTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
        var contentWidth = width - margin * 2;
        var y = margin;

        DrawHeader(
            canvas,
            metadata,
            report,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            contentWidth,
            scale,
            ref y);

        DrawText(canvas, currency.Currency, margin, y + 5f * scale, 4f * scale, palette.Ink, boldTypeface);
        DrawText(canvas, l["z_section_summary"], margin + contentWidth, y + 5f * scale, 2.4f * scale, palette.Muted, regularTypeface, SKTextAlign.Right);
        y += 10f * scale;

        var gap = 3f * scale;
        var summaryWidth = (contentWidth - gap * 2) / 3;
        var summaryHeight = 18f * scale;
        var summaries = new[]
        {
            (l["expected_cash"], currency.ExpectedCash, false),
            (l["counted_cash"], currency.CountedCash, false),
            (l["difference"], currency.Difference, true)
        };
        for (var index = 0; index < summaries.Length; index++)
        {
            var cardX = margin + index * (summaryWidth + gap);
            var background = summaries[index].Item3 ? palette.StatusBackground : palette.Faint;
            var border = summaries[index].Item3 ? palette.Status : palette.Faint;
            var foreground = summaries[index].Item3 ? palette.Status : palette.Ink;
            DrawRoundRect(
                canvas,
                new SKRect(cardX, y, cardX + summaryWidth, y + summaryHeight),
                background,
                border,
                0.35f * scale,
                1.2f * scale);
            DrawText(canvas, summaries[index].Item1, cardX + 4f * scale, y + 6f * scale, 2.2f * scale, palette.Muted, regularTypeface);
            DrawText(canvas, FormatAmount(summaries[index].Item2), cardX + 4f * scale, y + 13f * scale, 3.5f * scale, foreground, boldTypeface);
        }
        y += summaryHeight + 5f * scale;

        var detailHeight = 65f * scale;
        var cardWidth = (contentWidth - gap) / 2;
        DrawRoundRect(
            canvas,
            new SKRect(margin, y, margin + cardWidth, y + detailHeight),
            SKColors.White,
            palette.Line,
            0.35f * scale,
            1.2f * scale);
        DrawRoundRect(
            canvas,
            new SKRect(margin + cardWidth + gap, y, margin + contentWidth, y + detailHeight),
            SKColors.White,
            palette.Line,
            0.35f * scale,
            1.2f * scale);

        DrawSection(
            canvas,
            l["z_section_sales"],
            [
                (l["cash_sales"], FormatAmount(currency.CashSales)),
                (l["cash_returns"], FormatNegative(currency.CashReturns))
            ],
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            y,
            cardWidth,
            scale);
        DrawSection(
            canvas,
            l["z_section_cash"],
            [
                (l["opening_float"], FormatAmount(currency.OpeningFloat)),
                (l["debt_pay_in"], FormatAmount(currency.DebtPayIn)),
                (l["supply_pay_out"], FormatNegative(currency.SupplyPayOut))
            ],
            palette,
            regularTypeface,
            boldTypeface,
            margin + cardWidth + gap,
            y,
            cardWidth,
            scale);

        DrawFooter(
            canvas,
            metadata,
            l,
            palette,
            regularTypeface,
            boldTypeface,
            margin,
            height - margin - 9f * scale,
            contentWidth,
            9f * scale,
            scale);
        canvas.Flush();
        return Encode(bitmap);
    }

    private static void DrawSection(
        SKCanvas canvas,
        string title,
        IReadOnlyList<(string Label, string Value)> rows,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float y,
        float width,
        float scale)
    {
        var padding = 4f * scale;
        DrawRoundRect(
            canvas,
            new SKRect(x + padding, y + padding, x + 10f * scale, y + 10f * scale),
            palette.Faint,
            palette.Faint,
            0,
            1f * scale);
        DrawText(canvas, title, x + 12f * scale, y + 8.2f * scale, 3f * scale, palette.Ink, bold);

        var rowY = y + 16f * scale;
        var rowHeight = 6.7f * scale;
        foreach (var row in rows)
        {
            var isSubheading = string.IsNullOrEmpty(row.Value);
            if (isSubheading)
            {
                DrawLine(canvas, x + padding, rowY - 2.5f * scale, x + width - padding, rowY - 2.5f * scale, palette.Line, 0.25f * scale);
                DrawText(canvas, row.Label, x + padding, rowY + 1.1f * scale, 2.3f * scale, palette.Accent, bold);
            }
            else
            {
                DrawText(canvas, row.Label, x + padding, rowY, 2.35f * scale, palette.Muted, regular);
                DrawText(canvas, row.Value, x + width - padding, rowY, 2.45f * scale, palette.Ink, bold, SKTextAlign.Right);
            }
            rowY += rowHeight;
        }
    }

    private static void DrawFooter(
        SKCanvas canvas,
        ZReportDocumentMetadata metadata,
        LocalizationManager l,
        ZReportPalette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float y,
        float width,
        float height,
        float scale)
    {
        DrawLine(canvas, x, y, x + width, y, palette.Line, 0.3f * scale);
        DrawText(canvas, metadata.PrintedAt.ToString("dd.MM.yyyy HH:mm"), x, y + height * 0.67f, 2.1f * scale, palette.Muted, regular);
        var label = l["z_section_summary"];
        var labelWidth = MeasureText(label, 2.2f * scale, bold) + 6f * scale;
        var rect = new SKRect(x + width - labelWidth, y + 2f * scale, x + width, y + height - 0.5f * scale);
        DrawRoundRect(canvas, rect, palette.StatusBackground, palette.Status, 0.3f * scale, 0.9f * scale);
        DrawText(canvas, label, rect.MidX, y + height * 0.64f, 2.2f * scale, palette.Status, bold, SKTextAlign.Center);
    }

    private static string FormatAmount(decimal value) => value.ToString("N0");

    private static string FormatNegative(decimal value) => value == 0 ? "0" : $"-{value:N0}";

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static float MeasureText(string text, float size, SKTypeface typeface)
    {
        using var font = CreateFont(typeface, size);
        return font.MeasureText(text);
    }

    private static void DrawText(
        SKCanvas canvas,
        string text,
        float x,
        float baseline,
        float size,
        SKColor color,
        SKTypeface typeface,
        SKTextAlign align = SKTextAlign.Left)
    {
        using var font = CreateFont(typeface, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(text, x, baseline, align, font, paint);
    }

    private static SKFont CreateFont(SKTypeface typeface, float size) =>
        new(typeface, size)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = false
        };

    private static void DrawRoundRect(
        SKCanvas canvas,
        SKRect rect,
        SKColor background,
        SKColor border,
        float borderWidth,
        float radius)
    {
        using var fill = new SKPaint { Color = background, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(rect, radius, radius, fill);
        if (borderWidth <= 0)
            return;

        using var stroke = new SKPaint
        {
            Color = border,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth
        };
        canvas.DrawRoundRect(rect, radius, radius, stroke);
    }

    private static void DrawLine(
        SKCanvas canvas,
        float startX,
        float startY,
        float endX,
        float endY,
        SKColor color,
        float width)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true, StrokeWidth = width };
        canvas.DrawLine(startX, startY, endX, endY, paint);
    }

    private sealed record ZReportPalette(
        SKColor Ink,
        SKColor Muted,
        SKColor Line,
        SKColor Faint,
        SKColor Accent,
        SKColor Status,
        SKColor StatusBackground)
    {
        public static ZReportPalette Create(bool supportsColor) =>
            supportsColor
                ? new(
                    new SKColor(0x1E, 0x29, 0x3B),
                    new SKColor(0x64, 0x74, 0x8B),
                    new SKColor(0xCB, 0xD5, 0xE1),
                    new SKColor(0xF1, 0xF5, 0xF9),
                    new SKColor(0x25, 0x63, 0xEB),
                    new SKColor(0x16, 0xA3, 0x4A),
                    new SKColor(0xF0, 0xFD, 0xF4))
                : new(
                    new SKColor(0x11, 0x11, 0x11),
                    new SKColor(0x4B, 0x4B, 0x4B),
                    new SKColor(0xA3, 0xA3, 0xA3),
                    new SKColor(0xEE, 0xEE, 0xEE),
                    new SKColor(0x11, 0x11, 0x11),
                    new SKColor(0x11, 0x11, 0x11),
                    new SKColor(0xF3, 0xF3, 0xF3));
    }
}
