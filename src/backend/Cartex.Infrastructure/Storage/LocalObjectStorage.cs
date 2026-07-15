using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Configuration;

namespace Cartex.Infrastructure.Storage;

public sealed class LocalObjectStorage(ISettingsService settings, IConfiguration configuration) : IObjectStorage
{
    private readonly string _root = configuration["Storage:LocalPath"] is { Length: > 0 } path
        ? Path.GetFullPath(path)
        : Path.Combine(AppContext.BaseDirectory, "storage");

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    public Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null)
    {
        Directory.CreateDirectory(_root);
        key ??= $"{Guid.NewGuid():N}{extension}";
        var path = PathFor(key) ?? throw new ArgumentException("Invalid storage key.", nameof(key));
        using var file = File.Create(path);
        content.CopyTo(file);
        return Task.FromResult(key);
    }

    public async Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var s = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        return s is { Enabled: true } ? ContentUrl(key) : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        var distinct = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (distinct.Count == 0)
            return result;

        var s = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        if (s is not { Enabled: true })
            return result;

        foreach (var key in distinct)
            result[key] = ContentUrl(key);

        return result;
    }

    public Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult<(Stream, string)?>(null);

        var path = PathFor(key);
        if (path is null || !File.Exists(path))
            return Task.FromResult<(Stream, string)?>(null);

        var contentType = ContentTypes.GetValueOrDefault(Path.GetExtension(key), "application/octet-stream");
        return Task.FromResult<(Stream, string)?>((File.OpenRead(path), contentType));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(key) && PathFor(key) is { } path && File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ContentUrl(string key) => $"/api/storage/content?key={Uri.EscapeDataString(key)}";

    private string? PathFor(string key)
    {
        var root = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, key));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
