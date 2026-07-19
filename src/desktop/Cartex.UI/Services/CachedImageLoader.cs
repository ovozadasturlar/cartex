using System.Collections.Concurrent;
using System.Threading.Tasks;
using AsyncImageLoader.Loaders;
using Avalonia.Media.Imaging;

namespace Cartex.UI.Services;

public sealed class CachedImageLoader(string cacheFolder) : DiskCachedWebImageLoader(cacheFolder)
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _ram = new();

    public override async Task<Bitmap?> ProvideImageAsync(string url)
    {
        var bitmap = await _ram.GetOrAdd(url, base.ProvideImageAsync);
        if (bitmap is null) _ram.TryRemove(url, out _);
        return bitmap;
    }
}
