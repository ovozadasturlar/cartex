using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace Cartex.UI.Services;

public static class TsplLabel
{
    public record PreviewResult(byte[] Image, bool MayClip);

    public static byte[] Build(string code, string name, int quantity, LabelOptions options, string? priceText = null)
    {
        var dotsPerMm = options.Dpi / 25.4;
        var widthDots = (int)Math.Round(options.WidthMm * dotsPerMm + 7) / 8 * 8;
        var heightDots = (int)Math.Round(options.HeightMm * dotsPerMm);
        using var bitmap = RenderBitmap(code, name, priceText, widthDots, heightDots, dotsPerMm, options);

        using var stream = new MemoryStream();
        void Command(string text) => Write(stream, text + "\r\n");

        Command($"SIZE {Mm(options.WidthMm)} mm,{Mm(options.HeightMm)} mm");
        if (!options.UsePrinterGapCalibration)
            Command($"GAP {Mm(options.GapMm)} mm,0 mm");
        Command("DIRECTION 1");
        Command("REFERENCE 0,0");
        Command($"DENSITY {options.Density}");
        Command($"SPEED {options.Speed}");
        Command("CLS");
        Write(stream, $"BITMAP 0,0,{widthDots / 8},{heightDots},0,");
        stream.Write(ToMonochrome(bitmap.PeekPixels(), widthDots, heightDots));
        Write(stream, "\r\n");
        Command($"PRINT {Math.Clamp(quantity, 1, 999)},1");

        return stream.ToArray();
    }

    public static byte[] BuildCalibration(LabelOptions options)
    {
        using var stream = new MemoryStream();
        void Command(string text) => Write(stream, text + "\r\n");

        Command($"SIZE {Mm(options.WidthMm)} mm,{Mm(options.HeightMm)} mm");
        Command("DIRECTION 1");
        Command("REFERENCE 0,0");
        Command("GAPDETECT");
        Command("HOME");
        return stream.ToArray();
    }

    public static byte[] RenderPng(string code, string name, string? priceText, LabelOptions options)
        => RenderPreview(code, name, priceText, options).Image;

    public static PreviewResult RenderPreview(string code, string name, string? priceText, LabelOptions options)
    {
        var dotsPerMm = options.Dpi / 25.4;
        var widthDots = (int)Math.Round(options.WidthMm * dotsPerMm + 7) / 8 * 8;
        var heightDots = (int)Math.Round(options.HeightMm * dotsPerMm);
        using var bitmap = RenderBitmap(code, name, priceText, widthDots, heightDots, dotsPerMm, options);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new PreviewResult(
            data.ToArray(),
            InkTouchesEdge(bitmap, Math.Max(1, (int)Math.Round(dotsPerMm * 0.5))));
    }

    private static bool InkTouchesEdge(SKBitmap bitmap, int inset)
    {
        static bool IsInk(SKColor color) =>
            (color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000 < 128;

        var right = bitmap.Width - inset;
        var bottom = bitmap.Height - inset;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < inset; x++)
                if (IsInk(bitmap.GetPixel(x, y)))
                    return true;
            for (var x = right; x < bitmap.Width; x++)
                if (IsInk(bitmap.GetPixel(x, y)))
                    return true;
        }

        for (var x = inset; x < right; x++)
        {
            for (var y = 0; y < inset; y++)
                if (IsInk(bitmap.GetPixel(x, y)))
                    return true;
            for (var y = bottom; y < bitmap.Height; y++)
                if (IsInk(bitmap.GetPixel(x, y)))
                    return true;
        }

        return false;
    }

    private static string Mm(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void Write(Stream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));

    private static (int Left, int Right) InkBounds(SKBitmap bitmap)
    {
        int left = bitmap.Width, right = 0;
        for (var x = 0; x < bitmap.Width; x++)
            for (var y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y).Red < 128)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    break;
                }

        return right >= left ? (left, right) : (0, bitmap.Width - 1);
    }

    private static SKBitmap RenderBitmap(string code, string name, string? priceText, int widthDots, int heightDots, double dotsPerMm, LabelOptions options)
    {
        int Dots(double mm) => (int)Math.Round(mm * dotsPerMm);

        var bitmap = new SKBitmap(new SKImageInfo(widthDots, heightDots, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var shiftX = Dots(options.ShiftXMm);
        var shiftY = Dots(options.ShiftYMm);
        if (options.Rotation == 180)
        {
            canvas.Translate(widthDots + shiftX, heightDots + shiftY);
            canvas.RotateDegrees(180);
        }
        else
            canvas.Translate(-shiftX, -shiftY);

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        var sideMargin = Dots(2);
        var edgeMargin = Dots(2.5);
        var usable = widthDots - sideMargin * 2;

        float FontSize(double fraction, double minMm, double maxMm) => Math.Clamp((float)(heightDots * fraction), Dots(minMm), Dots(maxMm));
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, FontSize(0.075, 2.4, 4.4));
        using var priceFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, FontSize(0.13, 3.4, 5.5));
        using var codeFont = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, FontSize(0.075, 2.4, 3.3));

        var lines = WrapLines(name, nameFont, usable, string.IsNullOrWhiteSpace(priceText) ? 2 : 1);
        var nameHeight = lines.Count * nameFont.Spacing;
        var priceHeight = string.IsNullOrWhiteSpace(priceText) ? 0 : priceFont.Spacing;
        var codeHeight = codeFont.Spacing;
        var spacing = Dots(1);

        var contentTop = (float)edgeMargin;
        var contentBottom = heightDots - edgeMargin;
        var priceSpacing = priceHeight > 0 ? spacing : 0;
        var barcodeHeight = (int)Math.Max(Dots(7), contentBottom - contentTop - nameHeight - priceHeight - codeHeight - spacing * 2 - priceSpacing);
        var block = nameHeight + priceHeight + barcodeHeight + codeHeight + spacing * 2 + priceSpacing;
        var top = contentTop + (contentBottom - contentTop - block) / 2;

        foreach (var line in lines)
        {
            canvas.DrawText(line, widthDots / 2f, top + nameFont.Size, SKTextAlign.Center, nameFont, paint);
            top += nameFont.Spacing;
        }

        if (priceHeight > 0)
        {
            top += spacing;
            canvas.DrawText(priceText!, widthDots / 2f, top + priceFont.Size, SKTextAlign.Center, priceFont, paint);
            top += priceFont.Spacing;
        }

        using var barcode = RenderBarcode(code, usable, barcodeHeight);
        var (inkLeft, inkRight) = InkBounds(barcode);
        var barcodeX = (widthDots - (inkRight - inkLeft + 1)) / 2f - inkLeft;
        canvas.DrawBitmap(barcode, barcodeX, top + spacing);

        canvas.DrawText(code, widthDots / 2f, top + spacing + barcodeHeight + spacing + codeFont.Size, SKTextAlign.Center, codeFont, paint);
        canvas.Flush();

        return bitmap;
    }

    private static SKBitmap RenderBarcode(string code, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions { Width = width, Height = height, Margin = 0, PureBarcode = true }
        };
        var data = writer.Write(code);

        var bitmap = new SKBitmap(new SKImageInfo(data.Width, data.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        Marshal.Copy(data.Pixels, 0, bitmap.GetPixels(), data.Pixels.Length);
        return bitmap;
    }

    private static List<string> WrapLines(string text, SKFont font, float maxWidth, int maxLines)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return lines;

        var current = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (font.MeasureText(candidate) <= maxWidth) { current = candidate; continue; }

            if (current.Length > 0) lines.Add(current);
            if (lines.Count == maxLines) { current = string.Empty; break; }
            current = word;
        }

        if (current.Length > 0 && lines.Count < maxLines) lines.Add(current);

        for (var i = 0; i < lines.Count; i++)
            while (lines[i].Length > 1 && font.MeasureText(lines[i]) > maxWidth)
                lines[i] = lines[i][..^1];

        return lines;
    }

    private static byte[] ToMonochrome(SKPixmap pixmap, int width, int height)
    {
        var stride = width / 8;
        var output = new byte[stride * height];
        Array.Fill(output, (byte)0xFF);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = pixmap.GetPixelColor(x, y);
                var luma = (color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000;
                if (luma < 128)
                    output[y * stride + x / 8] &= (byte)~(0x80 >> (x % 8));
            }

        return output;
    }
}
