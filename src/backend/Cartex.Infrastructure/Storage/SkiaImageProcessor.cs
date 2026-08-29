using Cartex.Application.Common.Interfaces;
using SkiaSharp;

namespace Cartex.Infrastructure.Storage;

public sealed class SkiaImageProcessor : IImageProcessor
{
    private const int DisplaySide = 1200;
    private const int ThumbSide = 240;

    public ProcessedImage? Process(Stream original) => ProcessCore(original, monochrome: false);

    public ProcessedImage? ProcessMonochrome(Stream original) => ProcessCore(original, monochrome: true);

    private static ProcessedImage? ProcessCore(Stream original, bool monochrome)
    {
        using var data = SKData.Create(original);
        if (data is null)
            return null;

        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount > 1)
            return null;

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
            return null;

        var bitmap = Orient(decoded, codec.EncodedOrigin);
        if (monochrome)
        {
            var converted = ToMonochrome(bitmap);
            if (!ReferenceEquals(bitmap, decoded)) bitmap.Dispose();
            bitmap = converted;
        }
        try
        {
            var alpha = HasAlpha(bitmap);
            var display = Encode(bitmap, DisplaySide, alpha);
            var thumb = Encode(bitmap, ThumbSide, alpha);
            return new ProcessedImage(display, thumb, alpha ? "image/png" : "image/jpeg", alpha ? ".png" : ".jpg");
        }
        finally
        {
            if (!ReferenceEquals(bitmap, decoded))
                bitmap.Dispose();
        }
    }

    private static SKBitmap ToMonochrome(SKBitmap source)
    {
        var target = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, source.AlphaType);
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var color = source.GetPixel(x, y);
            var luminance = (int)Math.Round(color.Red * 0.299 + color.Green * 0.587 + color.Blue * 0.114);
            // A small contrast lift keeps thin logo strokes legible on thermal paper.
            var gray = (byte)Math.Clamp((luminance - 128) * 1.15 + 128, 0, 255);
            target.SetPixel(x, y, new SKColor(gray, gray, gray, color.Alpha));
        }
        return target;
    }

    private static bool HasAlpha(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
            return false;
        foreach (var pixel in bitmap.Pixels)
            if (pixel.Alpha < 255)
                return true;
        return false;
    }

    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return src;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var dst = new SKBitmap(swap ? src.Height : src.Width, swap ? src.Width : src.Height, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1);
                canvas.Translate(-src.Width, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180);
                canvas.Translate(-src.Width, -src.Height);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1);
                canvas.Translate(0, -src.Height);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.RotateDegrees(90);
                canvas.Translate(0, -src.Height);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.RotateDegrees(-90);
                canvas.Scale(1, -1);
                canvas.Translate(-src.Width, -src.Height);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.RotateDegrees(-90);
                canvas.Translate(-src.Width, 0);
                break;
        }
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }

    private static byte[] Encode(SKBitmap src, int maxSide, bool alpha)
    {
        var scale = Math.Min(1f, (float)maxSide / Math.Max(src.Width, src.Height));
        var bitmap = src;
        if (scale < 1f)
        {
            var info = new SKImageInfo(
                Math.Max(1, (int)Math.Round(src.Width * scale)),
                Math.Max(1, (int)Math.Round(src.Height * scale)));
            bitmap = src.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(alpha ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, alpha ? 100 : 82);
            return encoded.ToArray();
        }
        finally
        {
            if (!ReferenceEquals(bitmap, src))
                bitmap.Dispose();
        }
    }
}
