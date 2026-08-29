using Cartex.Shared.Models.Business;
using Cartex.Shared.Models.Settings;
using SkiaSharp;

namespace Cartex.UI.Services;

public static class ProformaDocumentRenderer
{
    private const double Dpi = 150;
    private const double MillimetersPerInch = 25.4;

    public static IReadOnlyList<byte[]> Render(
        PreviewDocument document,
        string? reference,
        BusinessDto? business,
        ProformaPrintOptions options,
        string paperSize,
        bool supportsColor,
        string? title = null)
    {
        var banner = title ?? LocalizationManager.Instance["preview_not_receipt"];
        var dimensions = DocumentPrintLayout.GetPaperDimensions(paperSize, "portrait");
        var scale = (float)(Dpi / MillimetersPerInch);
        var width = Math.Max(1, (int)Math.Round(dimensions.WidthMm * scale));
        var height = Math.Max(1, (int)Math.Round(dimensions.HeightMm * scale));
        var margin = (paperSize == "a5" ? 11f : 15f) * scale;
        var contentWidth = width - margin * 2;
        var palette = Palette.Create(supportsColor);
        using var regular = SKTypeface.FromFamilyName("Segoe UI");
        using var bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);

        var pages = new List<byte[]>();
        var lineIndex = 0;
        var pageNumber = 0;
        while (true)
        {
            pageNumber++;
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            var y = margin;
            if (pageNumber == 1)
                DrawHeader(canvas, document, reference, business, options, banner, palette, regular, bold, margin, contentWidth, scale, ref y);
            else
                DrawContinuationHeader(canvas, banner, palette, bold, margin, contentWidth, scale, pageNumber, ref y);

            DrawTableHeader(canvas, palette, bold, margin, contentWidth, scale, ref y);
            var rowHeight = 6f * scale;
            var bottom = height - margin;
            var totalsHeight = TotalsHeight(document, options, scale);
            var drawnOnPage = 0;
            while (lineIndex < document.Lines.Count)
            {
                var reserved = lineIndex == document.Lines.Count - 1 ? totalsHeight : 0;
                if (drawnOnPage > 0 && y + rowHeight + reserved > bottom) break;
                DrawRow(canvas, document.Lines[lineIndex], lineIndex, palette, regular, margin, contentWidth, scale, ref y);
                lineIndex++;
                drawnOnPage++;
            }

            if (lineIndex >= document.Lines.Count)
            {
                DrawTotals(canvas, document, options, banner, palette, regular, bold, margin, contentWidth, scale, ref y);
                pages.Add(Encode(bitmap));
                break;
            }
            pages.Add(Encode(bitmap));
        }
        return pages;
    }

    private static void DrawHeader(
        SKCanvas canvas,
        PreviewDocument document,
        string? reference,
        BusinessDto? business,
        ProformaPrintOptions options,
        string banner,
        Palette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var top = y;
        DrawRoundRect(canvas, new SKRect(x, y, x + 1.4f * scale, y + 13f * scale), palette.Accent, palette.Accent, 0, 0.7f * scale);
        var textX = x + 4.5f * scale;
        var leftY = y + 5f * scale;
        if (options.ShowBusinessName && !string.IsNullOrWhiteSpace(business?.Name))
        {
            DrawText(canvas, business.Name, textX, leftY, 4.2f * scale, palette.Ink, bold);
            leftY += 4.2f * scale;
        }
        if (options.ShowAddress && !string.IsNullOrWhiteSpace(business?.Address))
        {
            DrawText(canvas, business.Address, textX, leftY, 2.4f * scale, palette.Muted, regular);
            leftY += 3.2f * scale;
        }
        if (options.ShowPhone && !string.IsNullOrWhiteSpace(business?.Phone))
        {
            DrawText(canvas, business.Phone, textX, leftY, 2.4f * scale, palette.Muted, regular);
            leftY += 3.2f * scale;
        }

        var rightY = y + 4.5f * scale;
        DrawText(canvas, banner, x + width, rightY, 3.6f * scale, palette.Accent, bold, SKTextAlign.Right);
        rightY += 4f * scale;
        DrawText(canvas, document.CreatedAt.ToString("dd.MM.yyyy HH:mm"), x + width, rightY, 2.4f * scale, palette.Muted, regular, SKTextAlign.Right);
        rightY += 3.4f * scale;
        if (options.ShowCartCode && !string.IsNullOrWhiteSpace(reference))
        {
            DrawText(canvas, reference, x + width, rightY, 2.8f * scale, palette.Ink, bold, SKTextAlign.Right);
            rightY += 3.6f * scale;
        }
        if (options.ShowSeller && !string.IsNullOrWhiteSpace(document.UserName))
        {
            DrawText(canvas, $"Sotuvchi: {document.UserName}", x + width, rightY, 2.4f * scale, palette.Muted, regular, SKTextAlign.Right);
            rightY += 3.2f * scale;
        }
        if (options.ShowCustomer && !string.IsNullOrWhiteSpace(document.CustomerName))
        {
            DrawText(canvas, $"Mijoz: {document.CustomerName}", x + width, rightY, 2.4f * scale, palette.Muted, regular, SKTextAlign.Right);
            rightY += 3.2f * scale;
        }

        y = Math.Max(Math.Max(leftY, rightY), top + 14f * scale);
        if (!string.IsNullOrWhiteSpace(options.HeaderText))
        {
            foreach (var line in Wrap(options.HeaderText, width, 2.4f * scale, regular))
            {
                DrawText(canvas, line, x, y, 2.4f * scale, palette.Muted, regular);
                y += 3.2f * scale;
            }
        }
        y += 1.5f * scale;
        DrawLine(canvas, x, y, x + width, y, palette.Ink, 0.5f * scale);
        y += 3f * scale;
    }

    private static void DrawContinuationHeader(
        SKCanvas canvas,
        string banner,
        Palette palette,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        int pageNumber,
        ref float y)
    {
        DrawText(canvas, banner, x, y + 3.5f * scale, 3f * scale, palette.Accent, bold);
        DrawText(canvas, $"{pageNumber}", x + width, y + 3.5f * scale, 2.6f * scale, palette.Muted, bold, SKTextAlign.Right);
        y += 6f * scale;
        DrawLine(canvas, x, y, x + width, y, palette.Line, 0.3f * scale);
        y += 3f * scale;
    }

    private static void DrawTableHeader(
        SKCanvas canvas,
        Palette palette,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var l = LocalizationManager.Instance;
        var columns = Columns(x, width, scale);
        var baseline = y + 3.2f * scale;
        DrawText(canvas, "№", columns.Number, baseline, 2.6f * scale, palette.Ink, bold);
        DrawText(canvas, l["product"], columns.Name, baseline, 2.6f * scale, palette.Ink, bold);
        DrawText(canvas, l["quantity"], columns.Quantity, baseline, 2.6f * scale, palette.Ink, bold, SKTextAlign.Right);
        DrawText(canvas, l["price"], columns.Price, baseline, 2.6f * scale, palette.Ink, bold, SKTextAlign.Right);
        DrawText(canvas, l["amount"], columns.Amount, baseline, 2.6f * scale, palette.Ink, bold, SKTextAlign.Right);
        y += 4.6f * scale;
        DrawLine(canvas, x, y, x + width, y, palette.Line, 0.3f * scale);
        y += 1f * scale;
    }

    private static void DrawRow(
        SKCanvas canvas,
        PreviewLine line,
        int index,
        Palette palette,
        SKTypeface regular,
        float x,
        float width,
        float scale,
        ref float y)
    {
        var rowHeight = 6f * scale;
        if (index % 2 == 1)
            DrawRoundRect(canvas, new SKRect(x, y, x + width, y + rowHeight), palette.Faint, palette.Faint, 0, 0.8f * scale);
        var columns = Columns(x, width, scale);
        var baseline = y + 4f * scale;
        var size = 2.5f * scale;
        DrawText(canvas, (index + 1).ToString(), columns.Number, baseline, size, palette.Muted, regular);
        DrawText(canvas, Ellipsis($"{line.Name}", columns.NameWidth, size, regular), columns.Name, baseline, size, palette.Ink, regular);
        DrawText(canvas, $"{line.Quantity:0.###} {line.Unit}", columns.Quantity, baseline, size, palette.Ink, regular, SKTextAlign.Right);
        DrawText(canvas, $"{line.UnitPrice:N0}", columns.Price, baseline, size, palette.Ink, regular, SKTextAlign.Right);
        DrawText(canvas, $"{line.Amount:N0}", columns.Amount, baseline, size, palette.Ink, regular, SKTextAlign.Right);
        y += rowHeight;
    }

    private static float TotalsHeight(PreviewDocument document, ProformaPrintOptions options, float scale)
    {
        var height = 16f * scale;
        if (document.Discount > 0) height += 4f * scale;
        if (options.ShowNote && !string.IsNullOrWhiteSpace(document.Note)) height += 8f * scale;
        if (!string.IsNullOrWhiteSpace(options.FooterText)) height += 4f * scale;
        return height;
    }

    private static void DrawTotals(
        SKCanvas canvas,
        PreviewDocument document,
        ProformaPrintOptions options,
        string banner,
        Palette palette,
        SKTypeface regular,
        SKTypeface bold,
        float x,
        float width,
        float scale,
        ref float y)
    {
        y += 1f * scale;
        DrawLine(canvas, x, y, x + width, y, palette.Line, 0.3f * scale);
        y += 4.5f * scale;
        if (document.Discount > 0)
        {
            DrawText(canvas, "Chegirma", x + width - 42f * scale, y, 2.6f * scale, palette.Muted, regular, SKTextAlign.Left);
            DrawText(canvas, $"-{document.Discount:N0}", x + width, y, 2.6f * scale, palette.Ink, regular, SKTextAlign.Right);
            y += 4f * scale;
        }
        DrawText(canvas, LocalizationManager.Instance["total"], x + width - 42f * scale, y, 3.4f * scale, palette.Ink, bold, SKTextAlign.Left);
        DrawText(canvas, $"{document.Total:N0}", x + width, y, 3.4f * scale, palette.Ink, bold, SKTextAlign.Right);
        y += 5.5f * scale;
        if (options.ShowNote && !string.IsNullOrWhiteSpace(document.Note))
        {
            foreach (var line in Wrap($"Izoh: {document.Note}", width, 2.4f * scale, regular))
            {
                DrawText(canvas, line, x, y, 2.4f * scale, palette.Muted, regular);
                y += 3.2f * scale;
            }
            y += 1f * scale;
        }
        if (!string.IsNullOrWhiteSpace(options.FooterText))
        {
            DrawText(canvas, options.FooterText, x + width / 2, y, 2.4f * scale, palette.Muted, regular, SKTextAlign.Center);
            y += 4f * scale;
        }
        var bannerWidth = MeasureText(banner, 2.8f * scale, bold) + 8f * scale;
        var bannerRect = new SKRect(x + (width - bannerWidth) / 2, y, x + (width + bannerWidth) / 2, y + 6f * scale);
        DrawRoundRect(canvas, bannerRect, SKColors.White, palette.Accent, 0.35f * scale, 1f * scale);
        DrawText(canvas, banner, x + width / 2, y + 4f * scale, 2.8f * scale, palette.Accent, bold, SKTextAlign.Center);
        y += 8f * scale;
    }

    private static (float Number, float Name, float NameWidth, float Quantity, float Price, float Amount) Columns(
        float x,
        float width,
        float scale)
    {
        var amount = x + width;
        var price = amount - 30f * scale;
        var quantity = price - 26f * scale;
        var name = x + 9f * scale;
        return (x + 1f * scale, name, quantity - 24f * scale - name, quantity, price, amount);
    }

    private static string Ellipsis(string text, float maxWidth, float size, SKTypeface typeface)
    {
        if (MeasureText(text, size, typeface) <= maxWidth) return text;
        var result = text;
        while (result.Length > 1 && MeasureText(result + "…", size, typeface) > maxWidth)
            result = result[..^1];
        return result + "…";
    }

    private static IEnumerable<string> Wrap(string text, float maxWidth, float size, SKTypeface typeface)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (MeasureText(candidate, size, typeface) > maxWidth && current.Length > 0)
            {
                yield return current;
                current = word;
            }
            else
            {
                current = candidate;
            }
        }
        if (current.Length > 0) yield return current;
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static float MeasureText(string text, float size, SKTypeface typeface)
    {
        using var font = new SKFont(typeface, size);
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
        using var font = new SKFont(typeface, size);
        font.Edging = SKFontEdging.Antialias;
        font.Subpixel = false;
        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsAntialias = true;
        canvas.DrawText(text, x, baseline, align, font, paint);
    }

    private static void DrawRoundRect(SKCanvas canvas, SKRect rect, SKColor background, SKColor border, float borderWidth, float radius)
    {
        using var fill = new SKPaint();
        fill.Color = background;
        fill.IsAntialias = true;
        fill.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rect, radius, radius, fill);
        if (borderWidth <= 0) return;
        using var stroke = new SKPaint();
        stroke.Color = border;
        stroke.IsAntialias = true;
        stroke.Style = SKPaintStyle.Stroke;
        stroke.StrokeWidth = borderWidth;
        canvas.DrawRoundRect(rect, radius, radius, stroke);
    }

    private static void DrawLine(SKCanvas canvas, float startX, float startY, float endX, float endY, SKColor color, float width)
    {
        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsAntialias = true;
        paint.StrokeWidth = width;
        canvas.DrawLine(startX, startY, endX, endY, paint);
    }

    private sealed record Palette(SKColor Ink, SKColor Muted, SKColor Line, SKColor Faint, SKColor Accent)
    {
        public static Palette Create(bool supportsColor) =>
            supportsColor
                ? new(
                    new SKColor(0x1E, 0x29, 0x3B),
                    new SKColor(0x64, 0x74, 0x8B),
                    new SKColor(0xCB, 0xD5, 0xE1),
                    new SKColor(0xF1, 0xF5, 0xF9),
                    new SKColor(0x25, 0x63, 0xEB))
                : new(
                    new SKColor(0x11, 0x11, 0x11),
                    new SKColor(0x4B, 0x4B, 0x4B),
                    new SKColor(0xA3, 0xA3, 0xA3),
                    new SKColor(0xEE, 0xEE, 0xEE),
                    new SKColor(0x11, 0x11, 0x11));
    }
}
