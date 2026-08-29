using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Infrastructure.Storage;

public sealed class RoutedObjectStorage(ISettingsService settings, LocalObjectStorage local, MinioObjectStorage minio) : IObjectStorage
{
    private async Task<IObjectStorage> ResolveAsync(CancellationToken cancellationToken)
    {
        var s = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        return s?.Provider == "minio" ? minio : local;
    }

    public async Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null) =>
        await (await ResolveAsync(cancellationToken)).UploadAsync(content, length, contentType, extension, cancellationToken, key);

    public async Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default) =>
        await (await ResolveAsync(cancellationToken)).GetUrlAsync(key, cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await (await ResolveAsync(cancellationToken)).GetUrlsAsync(keys, cancellationToken);

    public async Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        var provider = await ResolveAsync(cancellationToken);
        var result = await provider.DownloadAsync(key, cancellationToken);
        if (result is not null)
            return result;

        // Existing installations may have switched providers after files were
        // uploaded. Reads must therefore check the other configured provider
        // as well; the public content endpoint is shared by web and mobile.
        IObjectStorage fallback = ReferenceEquals(provider, minio) ? local : minio;
        return await fallback.DownloadAsync(key, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var provider = await ResolveAsync(cancellationToken);
        await provider.DeleteAsync(key, cancellationToken);
        if (ReferenceEquals(provider, minio))
            await local.DeleteAsync(key, cancellationToken);
    }
}
