using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common.Exceptions;
using Minio;
using Minio.DataModel.Args;

namespace Cartex.Infrastructure.Storage;

public sealed class MinioObjectStorage(ISettingsService settings, ISecretProtector protector) : IObjectStorage
{
    public async Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null)
    {
        var (client, s) = await CreateAsync(cancellationToken);

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(s.Bucket), cancellationToken);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(s.Bucket), cancellationToken);

        key ??= $"{Guid.NewGuid():N}{extension}";
        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(s.Bucket)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType(contentType), cancellationToken);

        return key;
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

    public async Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        IMinioClient client;
        StorageSettings s;
        try { (client, s) = await CreateAsync(cancellationToken); }
        catch (BusinessRuleException) { return null; }

        try
        {
            var stat = await client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(s.Bucket)
                .WithObject(key), cancellationToken);

            var buffer = new MemoryStream();
            await client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(s.Bucket)
                .WithObject(key)
                .WithCallbackStream(async (stream, ct) => await stream.CopyToAsync(buffer, ct)), cancellationToken);

            buffer.Position = 0;
            return (buffer, string.IsNullOrWhiteSpace(stat.ContentType) ? "application/octet-stream" : stat.ContentType);
        }
        catch (Minio.Exceptions.MinioException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        IMinioClient client;
        StorageSettings s;
        try { (client, s) = await CreateAsync(cancellationToken); }
        catch (BusinessRuleException) { return; }

        try
        {
            await client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(s.Bucket)
                .WithObject(key), cancellationToken);
        }
        catch (Minio.Exceptions.MinioException)
        {
        }
    }

    private static string ContentUrl(string key) => $"/api/storage/content?key={Uri.EscapeDataString(key)}";

    private async Task<(IMinioClient Client, StorageSettings Settings)> CreateAsync(CancellationToken cancellationToken)
    {
        var s = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        if (s is null || !s.Enabled || string.IsNullOrWhiteSpace(s.Endpoint) || string.IsNullOrWhiteSpace(s.Bucket))
            throw new BusinessRuleException("Rasm xotirasi (storage) sozlanmagan.");

        var client = new MinioClient()
            .WithEndpoint(s.Endpoint)
            .WithCredentials(s.AccessKey, Reveal(s.SecretKey))
            .WithSSL(s.UseSsl)
            .Build();

        return (client, s);
    }

    private string? Reveal(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return secret;
        try { return protector.Unprotect(secret); }
        catch { return secret; }
    }
}
