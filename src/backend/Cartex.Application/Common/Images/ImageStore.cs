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
}
