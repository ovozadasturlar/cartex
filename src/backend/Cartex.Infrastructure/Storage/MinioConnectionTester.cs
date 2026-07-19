using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Domain.Common.Exceptions;
using Minio;
using Minio.DataModel.Args;

namespace Cartex.Infrastructure.Storage;

public sealed class MinioConnectionTester : IStorageConnectionTester
{
    public async Task EnsureReachableAsync(StorageSettings candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new MinioClient()
                .WithEndpoint(candidate.Endpoint)
                .WithCredentials(candidate.AccessKey, candidate.SecretKey)
                .WithSSL(candidate.UseSsl)
                .WithTimeout(5000)
                .Build();
            await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(candidate.Bucket), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new BusinessRuleException($"MinIO'ga ulanib bo'lmadi ({candidate.Endpoint}) — endpoint va kalitlarni tekshiring.");
        }
    }
}
