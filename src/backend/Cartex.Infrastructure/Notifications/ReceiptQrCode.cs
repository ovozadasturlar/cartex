using System.Runtime.InteropServices;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace Cartex.Infrastructure.Notifications;

internal static class ReceiptQrCode
{
    public static byte[] RenderPng(string content, int size = 480)
    {
        var pixels = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = size,
                Height = size,
                Margin = 1,
                PureBarcode = true
            }
        }.Write(content);

        using var bitmap = new SKBitmap(new SKImageInfo(
            pixels.Width,
            pixels.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        Marshal.Copy(pixels.Pixels, 0, bitmap.GetPixels(), pixels.Pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
