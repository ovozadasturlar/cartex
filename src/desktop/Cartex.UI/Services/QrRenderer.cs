using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ZXing;
using ZXing.Common;

namespace Cartex.UI.Services;

public static class QrRenderer
{
    public static Bitmap Render(string content, int size = 220)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = size, Height = size, Margin = 1 }
        };
        var data = writer.Write(content);
        var bitmap = new WriteableBitmap(new PixelSize(data.Width, data.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
            Marshal.Copy(data.Pixels, 0, buffer.Address, data.Pixels.Length);
        return bitmap;
    }
}
