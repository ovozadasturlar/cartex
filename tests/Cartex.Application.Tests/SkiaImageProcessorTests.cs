using Cartex.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace Cartex.Application.Tests;

public class SkiaImageProcessorTests
{
    private static MemoryStream JpegStream(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return new MemoryStream(data.ToArray());
    }

    private static (int Width, int Height) Size(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);
        return (bitmap.Width, bitmap.Height);
    }

    [Fact]
    public void Process_LargeJpeg_ResizesDisplayAndThumb()
    {
        using var stream = JpegStream(3000, 2000);
        var result = new SkiaImageProcessor().Process(stream);

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(".jpg", result.Extension);
        Assert.NotEmpty(result.Display);
        Assert.NotEmpty(result.Thumb);
        Assert.Equal((1200, 800), Size(result.Display));
        Assert.Equal((240, 160), Size(result.Thumb));
    }

    [Fact]
    public void Process_SmallImage_NotUpscaled()
    {
        using var stream = JpegStream(100, 80);
        var result = new SkiaImageProcessor().Process(stream);

        Assert.NotNull(result);
        Assert.Equal((100, 80), Size(result.Display));
        Assert.Equal((100, 80), Size(result.Thumb));
    }

    [Fact]
    public void Process_NonImage_ReturnsNull()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5]);
        Assert.Null(new SkiaImageProcessor().Process(stream));
    }
}
