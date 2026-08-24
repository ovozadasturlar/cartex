using Cartex.Shared.Models.Printing;
using SkiaSharp;

namespace Cartex.UI.Services;

public sealed record ReceiptRasterDocument(byte[] BeforeQr, byte[] AfterQr);

/// Termal boshning issiqligi va qog'oz sezgirligi har printerda har xil, shuning uchun
/// shtrix qalinligi va oq/qora chegarasi sozlanadi.
public sealed record ReceiptInk(float StrokeDots, int Threshold)
{
    public static ReceiptInk Resolve(string? key) => key?.ToLowerInvariant() switch
    {
        // Termal qog'oz nuqtani chetiga yoyadi, shuning uchun "light" 50% dan past
        // chegara bilan harfni ataylab ingichkalashtiradi.
        "light" => new ReceiptInk(0f, 112_000),
        "dark" => new ReceiptInk(0.5f, 148_000),
        _ => new ReceiptInk(0f, 128_000)
    };
}

/// Termal chiqish 1 bitli: sifat shrift chetlarini qanday hisoblashda hal bo'ladi.
/// "fast" — silliqlashsiz va qalinlashtirishsiz eng ingichka va eng tez;
/// "high" — uch barobar o'lchamda chizib, har nuqta uchun qoplamani o'rtachalash.
public sealed record ReceiptQuality(bool Antialias, int Supersample, bool Thicken)
{
    public static ReceiptQuality Resolve(string? key) => key?.ToLowerInvariant() switch
    {
        "fast" => new ReceiptQuality(false, 1, false),
        "high" => new ReceiptQuality(true, 3, true),
        _ => new ReceiptQuality(false, 1, true)
    };
}

public static class ReceiptRasterRenderer
{
    public static byte[] Render(string text, int dotWidth, string? darkness = null, string? quality = null) =>
        Render(ReceiptTextDocument.Plain(text, Math.Max(1, dotWidth / 12)), dotWidth, darkness, quality).BeforeQr;

    public static ReceiptRasterDocument Render(
        ReceiptTextDocument document,
        int dotWidth,
        string? darkness = null,
        string? quality = null)
    {
        var ink = ReceiptInk.Resolve(darkness);
        var mode = ReceiptQuality.Resolve(quality);
        var width = Align(Math.Max(8, dotWidth));
        using var regularTypeface = Monospace(SKFontStyle.Normal);
        using var boldTypeface = Monospace(SKFontStyle.Bold);
        var normalSize = FittedFontSize(regularTypeface, width, document.Width);
        var beforeHeight = Align(document.BeforeQr.Sum(line => LineHeight(line, normalSize)));
        var afterHeight = document.AfterQr.Count == 0
            ? 0
            : Align(document.AfterQr.Sum(line => LineHeight(line, normalSize)));
        var scale = mode.Supersample;
        using var bitmap = new SKBitmap(new SKImageInfo(
            width * scale,
            Math.Max(8, beforeHeight + afterHeight) * scale,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        DrawLines(canvas, document.BeforeQr, 0, width, normalSize, regularTypeface, boldTypeface, ink, mode);
        if (afterHeight > 0)
            DrawLines(canvas, document.AfterQr, beforeHeight, width, normalSize, regularTypeface, boldTypeface, ink, mode);
        canvas.Flush();
        return new ReceiptRasterDocument(
            ToRaster(ink, bitmap, 0, beforeHeight, width, scale),
            afterHeight == 0 ? [] : ToRaster(ink, bitmap, beforeHeight, afterHeight, width, scale));
    }

    private static void DrawLines(
        SKCanvas canvas,
        IReadOnlyList<ReceiptTextLine> lines,
        int top,
        int width,
        int normalSize,
        SKTypeface regularTypeface,
        SKTypeface boldTypeface,
        ReceiptInk ink,
        ReceiptQuality mode)
    {
        var s = mode.Supersample;
        var y = top;
        foreach (var line in lines)
        {
            var height = LineHeight(line, normalSize);
            if (line.Style is ReceiptTextStyle.Separator or ReceiptTextStyle.StrongSeparator)
            {
                using var separatorPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    StrokeWidth = (line.Style == ReceiptTextStyle.StrongSeparator ? 3 : 1) * s,
                    IsAntialias = false
                };
                canvas.DrawLine(4f * s, (y + height / 2f) * s, (width - 4) * s, (y + height / 2f) * s, separatorPaint);
                y += height;
                continue;
            }

            var size = line.Style switch
            {
                ReceiptTextStyle.Title => normalSize + 8,
                ReceiptTextStyle.Total => normalSize + 3,
                ReceiptTextStyle.Strong => normalSize + 1,
                _ => normalSize
            };
            // 203 dpi da bold shtrix ~3.5 nuqta bo'lib termal qog'ozda yoyilib dog' bo'ladi.
            // Qalin garnitura faqat bir marta uchraydigan sarlavha va teskari JAMI uchun.
            var typeface = line.Style is ReceiptTextStyle.Title or ReceiptTextStyle.Total
                ? boldTypeface
                : regularTypeface;
            using var font = new SKFont(typeface, size * s) { Edging = mode.Antialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias };
            if (font.MeasureText(line.Text) > (width - 16) * s)
            {
                var fitted = Math.Max(10f * s, (float)Math.Floor(font.Size * (width - 16) * s / font.MeasureText(line.Text)));
                font.Size = fitted;
            }
            var reverse = line.Style == ReceiptTextStyle.Total;
            if (reverse)
            {
                using var backgroundPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
                canvas.DrawRect(0, y * s, width * s, height * s, backgroundPaint);
            }
            // Termal bosh 203 dpi da bir piksellik shtrixni to'liq qoraytirmaydi va harfning
            // bir qismi tushib qoladi ("Toshkent" -> "Tosnkent"). Matn to'ldirish ustiga
            // ingichka chiziq bilan qalinlashtiriladi.
            var thicken = mode.Thicken ? ink.StrokeDots * s : 0f;
            using var paint = new SKPaint
            {
                Color = reverse ? SKColors.White : SKColors.Black,
                IsAntialias = mode.Antialias,
                Style = thicken > 0 ? SKPaintStyle.StrokeAndFill : SKPaintStyle.Fill,
                StrokeWidth = thicken
            };
            var align = line.Centered ? SKTextAlign.Center : SKTextAlign.Left;
            var x = line.Centered ? width * s / 2f : 8f * s;
            var baseline = y * s + (height * s - font.Metrics.Descent - font.Metrics.Ascent) / 2f;
            canvas.DrawText(line.Text, x, baseline, align, font, paint);
            y += height;
        }
    }

    /// Qator balandligi shrift o'lchamiga bog'liq: aks holda kattaroq yozuv o'z qutisiga
    /// sig'may qatorlar ustma-ust tushardi.
    private static int LineHeight(ReceiptTextLine line, int normalSize) => line.Style switch
    {
        ReceiptTextStyle.Title => (normalSize + 8) * 8 / 7,
        ReceiptTextStyle.Total => (normalSize + 3) * 8 / 7,
        ReceiptTextStyle.Separator => 8,
        ReceiptTextStyle.StrongSeparator => 8,
        _ when line.Text.Length == 0 => normalSize * 3 / 4,
        _ => normalSize * 8 / 7
    };

    /// Yuqori chegara qog'oz kengligidan kelib chiqadi, qat'iy sondan emas: kam ustunli
    /// chekda shrift qog'ozni to'ldirmasa o'ng tomonda bo'sh joy qolib ketardi.
    private static int FittedFontSize(SKTypeface typeface, int width, int columns)
    {
        var cellWidth = (width - 16f) / Math.Max(1, columns);
        for (var size = 72; size >= 12; size--)
        {
            using var font = new SKFont(typeface, size);
            if (font.MeasureText("0") <= cellWidth)
                return size;
        }
        return 12;
    }

    private static byte[] ToRaster(ReceiptInk ink, SKBitmap bitmap, int startY, int height, int width, int scale)
    {
        var widthBytes = width / 8;
        var data = new byte[widthBytes * height];
        var samples = scale * scale;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = Average(bitmap, x * scale, (startY + y) * scale, scale, samples);
                var luminance = color.Red * 299 + color.Green * 587 + color.Blue * 114;
                // Chegara 50% da silliqlangan chekka piksellar oqqa ketib, harf yupqalashadi.
                // Termal qog'oz uchun chegara qoraga suriladi.
                if (color.Alpha > 96 && luminance < ink.Threshold)
                    data[y * widthBytes + x / 8] |= (byte)(1 << (7 - x % 8));
            }

        var output = new byte[data.Length + 8];
        output[0] = 0x1D;
        output[1] = 0x76;
        output[2] = 0x30;
        output[3] = 0x00;
        output[4] = (byte)(widthBytes & 0xFF);
        output[5] = (byte)(widthBytes >> 8);
        output[6] = (byte)(height & 0xFF);
        output[7] = (byte)(height >> 8);
        Buffer.BlockCopy(data, 0, output, 8, data.Length);
        return output;
    }

    /// Ortiqcha o'lchamda chizilgan tasvirni bir nuqtaga siqish: har nuqta o'z blokidagi
    /// qoplamaning o'rtachasi bo'ladi, shuning uchun harf cheti aniqroq hisoblanadi.
    private static SKColor Average(SKBitmap bitmap, int x, int y, int scale, int samples)
    {
        if (scale == 1) return bitmap.GetPixel(x, y);
        int r = 0, g = 0, b = 0, a = 0;
        for (var dy = 0; dy < scale; dy++)
            for (var dx = 0; dx < scale; dx++)
            {
                var pixel = bitmap.GetPixel(x + dx, y + dy);
                r += pixel.Red; g += pixel.Green; b += pixel.Blue; a += pixel.Alpha;
            }
        return new SKColor((byte)(r / samples), (byte)(g / samples), (byte)(b / samples), (byte)(a / samples));
    }

    private static int Align(int value) => (value + 7) / 8 * 8;

    /// Chek ustunlari faqat teng kenglikdagi shriftda tekis turadi va o'zbek kirillchasining
    /// қ, ғ, ҳ harflari har shriftda ham yo'q — shuning uchun zaxira ro'yxati aniq beriladi.
    private static SKTypeface Monospace(SKFontStyle style)
    {
        foreach (var family in MonospaceFamilies)
            if (SKTypeface.FromFamilyName(family, style) is { } typeface
                && typeface.GetGlyph('қ') != 0 && typeface.GetGlyph('ғ') != 0 && typeface.GetGlyph('ҳ') != 0)
                return typeface;
        return SKTypeface.FromFamilyName(MonospaceFamilies[0], style) ?? SKTypeface.Default;
    }

    private static readonly string[] MonospaceFamilies = ["Consolas", "Cascadia Mono", "Courier New", "DejaVu Sans Mono"];
}
