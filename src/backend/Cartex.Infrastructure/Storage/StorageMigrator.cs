using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Infrastructure.Storage;

public sealed class StorageMigrator(IServiceScopeFactory scopes, IConfiguration configuration) : IStorageMigrator
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
    };

    private readonly Lock _lock = new();
    private volatile StorageMigrationStatus _status = new(false, null, 0, 0, 0, null, null);

    public StorageMigrationStatus Status => _status;

    public void Start(StorageMigrationDirection direction)
    {
        lock (_lock)
        {
            if (_status.Running)
                throw new BusinessRuleException("Ko'chirish allaqachon davom etmoqda.");
            _status = new StorageMigrationStatus(true, direction.ToString(), 0, 0, 0, null, null);
        }
        _ = Task.Run(() => RunAsync(direction));
    }

    private async Task RunAsync(StorageMigrationDirection direction)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var minio = scope.ServiceProvider.GetRequiredService<MinioObjectStorage>();
            if (direction == StorageMigrationDirection.LocalToRemote)
                await LocalToRemoteAsync(minio);
            else
                await RemoteToLocalAsync(minio);
            _status = _status with { Running = false, FinishedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _status = _status with { Running = false, Error = ex.Message, FinishedAt = DateTime.UtcNow };
        }
    }

    private string Root => configuration["Storage:LocalPath"] is { Length: > 0 } path
        ? Path.GetFullPath(path)
        : Path.Combine(AppContext.BaseDirectory, "storage");

    private async Task LocalToRemoteAsync(MinioObjectStorage minio)
    {
        var root = Root;
        if (!Directory.Exists(root))
            return;

        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
        _status = _status with { Total = files.Count };

        foreach (var file in files)
        {
            var key = Path.GetRelativePath(root, file).Replace('\\', '/');
            try
            {
                await using var stream = File.OpenRead(file);
                var contentType = ContentTypes.GetValueOrDefault(Path.GetExtension(file), "application/octet-stream");
                await minio.UploadAsync(stream, stream.Length, contentType, Path.GetExtension(file), CancellationToken.None, key);
                _status = _status with { Processed = _status.Processed + 1 };
            }
            catch (BusinessRuleException)
            {
                throw;
            }
            catch
            {
                _status = _status with { Failed = _status.Failed + 1 };
            }
        }
    }

    private async Task RemoteToLocalAsync(MinioObjectStorage minio)
    {
        var root = Root;
        var keys = await minio.ListKeysAsync();
        _status = _status with { Total = keys.Count };

        foreach (var key in keys)
        {
            try
            {
                var download = await minio.DownloadAsync(key);
                if (download is null) { _status = _status with { Failed = _status.Failed + 1 }; continue; }
                var target = Path.GetFullPath(Path.Combine(root, key));
                if (!target.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                { _status = _status with { Failed = _status.Failed + 1 }; continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using (download.Value.Content)
                await using (var file = File.Create(target))
                    await download.Value.Content.CopyToAsync(file);
                _status = _status with { Processed = _status.Processed + 1 };
            }
            catch (BusinessRuleException)
            {
                throw;
            }
            catch
            {
                _status = _status with { Failed = _status.Failed + 1 };
            }
        }
    }
}
