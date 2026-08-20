using Cartex.Shared.Models.OfflineCache;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IOfflineCacheApi
{
    [Get("/api/offline-cache")]
    Task<OfflineCacheStateDto> GetStateAsync();

    [Post("/api/offline-cache/claim")]
    Task<OfflineLeaseGrantDto> ClaimAsync([Body] ClaimOfflineCacheRequest request);

    [Post("/api/offline-cache/release")]
    Task ReleaseAsync([Body] ReleaseOfflineCacheRequest request);

    [Post("/api/offline-cache/heartbeat")]
    Task<OfflineHeartbeatDto> HeartbeatAsync([Body] OfflineHeartbeatRequest request);

    [Get("/api/offline-cache/snapshot")]
    Task<OfflineSnapshotDto> GetSnapshotAsync(
        [Query] long leaseId,
        [Query] long epoch,
        [Header("X-Offline-Lease-Token")] string leaseToken,
        [Query] string? sections = null,
        // Round-trip format shart: oddiy formatda UTC belgisi yo'qoladi va delta oynasi soatlarga siljiydi.
        [Query(Format = "o")] DateTime? since = null);

    [Post("/api/offline-cache/hub-attestation")]
    Task<HubAttestationDto> GetHubAttestationAsync([Body] HubAttestationRequest request);

    [Post("/api/offline-cache/sync/batches")]
    Task<OfflineSyncBatchResult> SyncBatchAsync([Body] OfflineSyncBatchRequest request);

    [Post("/api/offline-cache/sync/skip")]
    Task<OfflineSyncEventResult> SkipAsync([Body] OfflineSyncSkipRequest request);

    [Post("/api/offline-cache/sync/import")]
    Task<OfflineSyncBatchResult> ImportAsync([Body] OfflineSyncImportRequest request);
}
