using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace Cartex.UI.Services;

// Gprinter/TSC yorliq printerlari (GP-320TUD va shunga o'xshash) TSPL tilini tushunadi. Yorliq
// butunlay bitta monoxrom rasm sifatida chiziladi: TSPL ichki shriftlari lotin/kirill mahsulot
// nomlarini chiqara olmaydi, rasm esa har qanday shriftni va aniq joylashuvni saqlaydi.
public static class TsplLabel
{
    public static byte[] Build(string code, string name, int quantity, LabelOptions options)
    {
        var dotsPerMm = options.Dpi / 25.4;
        var widthDots = (int)Math.Round(options.WidthMm * dotsPerMm + 7) / 8 * 8;
        var heightDots = (int)Math.Round(options.HeightMm * dotsPerMm);
        var bitmap = Render(code, name, widthDots, heightDots, dotsPerMm, options);

        using var stream = new MemoryStream();
        void Command(string text) => Write(stream, text + "\r\n");

        Command($"SIZE {Mm(options.WidthMm)} mm,{Mm(options.HeightMm)} mm");
        Command($"GAP {Mm(options.GapMm)} mm,0 mm");
        Command("DIRECTION 1");
        Command("REFERENCE 0,0");
        Command($"DENSITY {options.Density}");
        Command($"SPEED {options.Speed}");
        Command("CLS");
        Write(stream, $"BITMAP 0,0,{widthDots / 8},{heightDots},0,");
        stream.Write(bitmap);
        Write(stream, "\r\n");
        Command($"PRINT {Math.Clamp(quantity, 1, 999)},1");

        return stream.ToArray();
    }

    // Printer rulonni o'zi o'lchaydi (bir necha yorliqni o'tkazib) va yorliq/bo'shliq o'lchamini xotirasiga yozadi.
    // Rulon almashtirilganda bir marta yuborilsa, keyingi chop etishlar bo'shliq qiymatiga bog'liq bo'lmay qoladi.
    public static byte[] BuildCalibration(LabelOptions options)
    {
        var tspl = $"SIZE {Mm(options.WidthMm)} mm,{Mm(options.HeightMm)} mm\r\nDIRECTION 1\r\nGAPDETECT\r\n";
        return Encoding.ASCII.GetBytes(tspl);
    }

    private static string Mm(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void Write(Stream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));

    // Shtrix-kod rasmidagi qora chiziqlarning chap va o'ng chegarasi.
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

    private static byte[] Render(string code, string name, int widthDots, int heightDots, double dotsPerMm, LabelOptions options)
    {
        int Dots(double mm) => (int)Math.Round(mm * dotsPerMm);

        using var surface = SKSurface.Create(new SKImageInfo(widthDots, heightDots, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        // Aylantirish TSPL DIRECTION bilan emas, rasmning o'zi orqali: DIRECTION 0 bosish boshlanish nuqtasini
        // ham siljitadi va tarkib qo'shni yorliqqa chiqib ketadi.
        // Surish sozlamada o'qish holatida beriladi: 180° da rasm koordinatalari teskari bo'lgani uchun ishorasi almashadi.
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
        var sideMargin = Dots(2);     // chap/o'ng chet — 2 mm
        var edgeMargin = Dots(2.5);   // tepa/past chet — 2.5 mm, raqam qog'oz chetiga tegib qolmasin
        var usable = widthDots - sideMargin * 2;

        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) ?? SKTypeface.Default, heightDots * 0.09f);
        using var codeFont = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, heightDots * 0.1f);

        var lines = WrapLines(name, nameFont, usable, 2);
        var nameHeight = lines.Count * nameFont.Spacing;
        var codeHeight = codeFont.Spacing;
        var spacing = Dots(1);

        // Tarkib chet chegaralari orasida vertikal markazlashadi: shtrix-kod qolgan bo'sh joyni to'ldiradi.
        var contentTop = (float)edgeMargin;
        var contentBottom = heightDots - edgeMargin;
        var barcodeHeight = (int)Math.Max(Dots(6), contentBottom - contentTop - nameHeight - codeHeight - spacing * 2);
        var block = nameHeight + spacing + barcodeHeight + spacing + codeHeight;
        var top = contentTop + (contentBottom - contentTop - block) / 2;

        foreach (var line in lines)
        {
            canvas.DrawText(line, widthDots / 2f, top + nameFont.Size, SKTextAlign.Center, nameFont, paint);
            top += nameFont.Spacing;
        }

        using var barcode = RenderBarcode(code, usable, barcodeHeight);
        var (inkLeft, inkRight) = InkBounds(barcode);
        // ZXing chetlarga teng bo'lmagan bo'sh joy qoldiradi — shuning uchun rasm emas, chiziqlarning o'zi markazlanadi.
        var barcodeX = (widthDots - (inkRight - inkLeft + 1)) / 2f - inkLeft;
        canvas.DrawBitmap(barcode, barcodeX, top + spacing);

        canvas.DrawText(code, widthDots / 2f, top + spacing + barcodeHeight + spacing + codeFont.Size, SKTextAlign.Center, codeFont, paint);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();
        return ToMonochrome(pixmap, widthDots, heightDots);
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
            if (lines.Count == maxLines) return lines;
            current = word;
        }

        if (current.Length > 0 && lines.Count < maxLines) lines.Add(current);

        for (var i = 0; i < lines.Count; i++)
            while (lines[i].Length > 1 && font.MeasureText(lines[i]) > maxWidth)
                lines[i] = lines[i][..^1];

        return lines;
    }

    // TSPL BITMAP: bit 0 — qora nuqta, bit 1 — bo'sh joy.
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
