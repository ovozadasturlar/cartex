using Cartex.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace Cartex.UnitTests;

public sealed class ImageProcessorTests
{
    [Fact]
    public void Monochrome_logo_has_equal_rgb_channels_and_keeps_alpha()
    {
        using var bitmap = new SKBitmap(2, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.SetPixel(0, 0, new SKColor(240, 30, 10, 255));
        bitmap.SetPixel(1, 0, new SKColor(10, 200, 60, 123));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var input = new MemoryStream(encoded.ToArray());

        var result = new SkiaImageProcessor().ProcessMonochrome(input);

        Assert.NotNull(result);
        using var output = SKBitmap.Decode(result.Display);
        for (var x = 0; x < output.Width; x++)
        {
            var pixel = output.GetPixel(x, 0);
            Assert.Equal(pixel.Red, pixel.Green);
            Assert.Equal(pixel.Green, pixel.Blue);
        }
        Assert.Equal((byte)123, output.GetPixel(1, 0).Alpha);
    }
}
