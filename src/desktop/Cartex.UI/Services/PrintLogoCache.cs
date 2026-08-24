using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Cartex.ApiClient.Api;

namespace Cartex.UI.Services;

public sealed class PrintLogoCache
{
    private static readonly HttpClient ImageHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly TimeSpan PrintWait = TimeSpan.FromMilliseconds(1500);
    private readonly IStorageApi _storage;
    private readonly IBusinessApi _business;
    private readonly IPrinterService _printer;
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, Task<byte[]>> _pending = new();

    public PrintLogoCache(IStorageApi storage, IBusinessApi business, IPrinterService printer)
    {
        _storage = storage;
        _business = business;
        _printer = printer;
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cartex",
            "print-logo-cache");
        Directory.CreateDirectory(_directory);
    }

    public async Task<byte[]?> GetForPrintAsync(
        string imageKey,
        int width,
        CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(imageKey, width);
        var path = Path.Combine(_directory, cacheKey + ".bin");
        if (File.Exists(path))
        {
            try
            {
                return await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        var task = _pending.GetOrAdd(cacheKey, _ => DownloadAsync(imageKey, width, cacheKey, path));
        try
        {
            return await task.WaitAsync(PrintWait, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or HttpRequestException
                                          || !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task WarmAsync(string? imageKey, int width)
    {
        if (string.IsNullOrWhiteSpace(imageKey)) return;
        try
        {
            var cacheKey = CacheKey(imageKey, width);
            var path = Path.Combine(_directory, cacheKey + ".bin");
            if (File.Exists(path)) return;
            await _pending.GetOrAdd(cacheKey, _ => DownloadAsync(imageKey, width, cacheKey, path));
        }
        catch
        {
            return;
        }
    }

    public async Task WarmCurrentAsync()
    {
        try
        {
            var business = await _business.GetAsync();
            var imageKey = !string.IsNullOrWhiteSpace(business.MonochromeLogoImageKey)
                ? business.MonochromeLogoImageKey
                : business.LogoImageKey;
            var target = _printer.ReceiptTarget();
            if (target.Printer is null) return;
            var width = _printer.ReceiptRasterWidth(target.Printer);
            await WarmAsync(imageKey, width);
        }
        catch
        {
            return;
        }
    }

    private async Task<byte[]> DownloadAsync(string imageKey, int width, string cacheKey, string path)
    {
        try
        {
            var file = await _storage.GetUrlAsync(imageKey);
            var url = ImageUrl.Absolute(file.Url) ?? throw new HttpRequestException("Logo URL is missing.");
            var image = await ImageHttpClient.GetByteArrayAsync(url);
            var raster = EscPosImageHelper.BinarizeToEscPosRaster(image, width);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporary, raster);
            File.Move(temporary, path, true);
            return raster;
        }
        finally
        {
            _pending.TryRemove(cacheKey, out _);
        }
    }

    private static string CacheKey(string imageKey, int width) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{imageKey}\n{width}")))
            .ToLowerInvariant();
}
