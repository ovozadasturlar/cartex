using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common.Exceptions;
using Minio;
using Minio.DataModel.Args;

namespace Cartex.Infrastructure.Storage;

public sealed class MinioObjectStorage(ISettingsService settings) : IObjectStorage
{
    public async Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default)
    {
        var (client, s) = await CreateAsync(cancellationToken);

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(s.Bucket), cancellationToken);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(s.Bucket), cancellationToken);

        var key = $"{Guid.NewGuid():N}{extension}";
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

        var (client, s) = await CreateAsync(cancellationToken);
        return await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(s.Bucket)
            .WithObject(key)
            .WithExpiry(3600));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>();
        var distinct = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (distinct.Count == 0)
            return result;

        IMinioClient client;
        StorageSettings s;
        try { (client, s) = await CreateAsync(cancellationToken); }
        catch (BusinessRuleException) { return result; }

        foreach (var key in distinct)
        {
            var url = await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(s.Bucket)
                .WithObject(key)
                .WithExpiry(3600));
            if (!string.IsNullOrWhiteSpace(url))
                result[key] = url;
        }

        return result;
    }

    private async Task<(IMinioClient Client, StorageSettings Settings)> CreateAsync(CancellationToken cancellationToken)
    {
        var s = await settings.GetAsync<StorageSettings>(SettingKeys.Storage, cancellationToken);
        if (s is null || !s.Enabled || string.IsNullOrWhiteSpace(s.Endpoint) || string.IsNullOrWhiteSpace(s.Bucket))
            throw new BusinessRuleException("Rasm xotirasi (storage) sozlanmagan.");

        var client = new MinioClient()
            .WithEndpoint(s.Endpoint)
            .WithCredentials(s.AccessKey, s.SecretKey)
            .WithSSL(s.UseSsl)
            .Build();

        return (client, s);
    }
}
