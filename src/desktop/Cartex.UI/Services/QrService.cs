using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ZXing;
using ZXing.Common;

namespace Cartex.UI.Services;

public static class QrService
{
    public static Bitmap? Generate(string? text, int size = 240)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = size, Height = size, Margin = 1 }
        };
        var pixels = writer.Write(text);

        var bitmap = new WriteableBitmap(new PixelSize(pixels.Width, pixels.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bitmap.Lock())
            Marshal.Copy(pixels.Pixels, 0, fb.Address, pixels.Pixels.Length);
        return bitmap;
    }
}
