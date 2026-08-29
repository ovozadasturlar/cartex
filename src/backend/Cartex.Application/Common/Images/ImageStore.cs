using Cartex.Application.Common.Interfaces;

namespace Cartex.Application.Common.Images;

public static class ImageStore
{
    public static async Task<string> SaveAsync(IObjectStorage storage, IImageProcessor processor,
        MemoryStream buffer, string contentType, string extension, CancellationToken cancellationToken)
    {
        var processed = processor.Process(buffer);
        if (processed is null)
        {
            buffer.Position = 0;
            return await storage.UploadAsync(buffer, buffer.Length, contentType, extension, cancellationToken);
        }

        using var display = new MemoryStream(processed.Display);
        var key = await storage.UploadAsync(display, processed.Display.Length, processed.ContentType, processed.Extension, cancellationToken);
        using var thumb = new MemoryStream(processed.Thumb);
        await storage.UploadAsync(thumb, processed.Thumb.Length, processed.ContentType, processed.Extension, cancellationToken, $"t_{key}");
        return key;
    }

    public static async Task<(string ColorKey, string MonochromeKey)> SaveLogoPairAsync(
        IObjectStorage storage,
        IImageProcessor processor,
        MemoryStream buffer,
        string contentType,
        string extension,
        CancellationToken cancellationToken)
    {
        buffer.Position = 0;
        var color = processor.Process(buffer);
        buffer.Position = 0;
        var monochrome = processor.ProcessMonochrome(buffer);

        var colorKey = color is null
            ? await UploadOriginalAsync(storage, buffer, contentType, extension, cancellationToken)
            : await UploadProcessedAsync(storage, color, cancellationToken);
        var monochromeKey = monochrome is null
            ? colorKey
            : await UploadProcessedAsync(storage, monochrome, cancellationToken);
        return (colorKey, monochromeKey);
    }

    private static async Task<string> UploadProcessedAsync(
        IObjectStorage storage,
        ProcessedImage image,
        CancellationToken cancellationToken)
    {
        using var display = new MemoryStream(image.Display);
        var key = await storage.UploadAsync(display, image.Display.Length, image.ContentType, image.Extension,
            cancellationToken);
        using var thumb = new MemoryStream(image.Thumb);
        await storage.UploadAsync(thumb, image.Thumb.Length, image.ContentType, image.Extension,
            cancellationToken, $"t_{key}");
        return key;
    }

    private static async Task<string> UploadOriginalAsync(
        IObjectStorage storage,
        MemoryStream buffer,
        string contentType,
        string extension,
        CancellationToken cancellationToken)
    {
        buffer.Position = 0;
        return await storage.UploadAsync(buffer, buffer.Length, contentType, extension, cancellationToken);
    }
}
