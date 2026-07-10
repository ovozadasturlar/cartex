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

    public async Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        await (await ResolveAsync(cancellationToken)).DownloadAsync(key, cancellationToken);

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await (await ResolveAsync(cancellationToken)).DeleteAsync(key, cancellationToken);
}
