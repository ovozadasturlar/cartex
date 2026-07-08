using Cartex.Shared.Models.OfflineCache;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IOfflineCacheApi
{
    [Get("/api/offline-cache")]
    Task<OfflineCacheStateDto> GetStateAsync();

    [Post("/api/offline-cache/claim")]
    Task ClaimAsync([Body] ClaimOfflineCacheRequest request);

    [Post("/api/offline-cache/release")]
    Task ReleaseAsync();

    [Get("/api/offline-cache/snapshot")]
    Task<OfflineSnapshotDto> GetSnapshotAsync([Query] long warehouseId, [Query] string deviceId);
}
