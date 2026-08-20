using Cartex.Application.Common.Messaging;
using Cartex.Application.OfflineCache.Commands;
using Cartex.Application.OfflineCache.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/offline-cache")]
[Authorize]
[RequiresFeature(FeatureCatalog.OfflineCache)]
public class OfflineCacheController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Devices.View)]
    public async Task<ActionResult<OfflineCacheStateDto>> GetState()
    {
        var state = await sender.Send(new GetOfflineCacheStateQuery());
        return Ok(state);
    }

    [HttpPost("claim")]
    [HasPermission(AppPermissions.Devices.Revoke)]
    public async Task<ActionResult<OfflineLeaseGrantDto>> Claim(ClaimOfflineCacheRequest request)
    {
        var result = await sender.Send(new ClaimOfflineCacheCommand(
            request.DeviceId, request.DeviceName, request.WarehouseId));
        return Ok(result);
    }

    [HttpPost("release")]
    [HasPermission(AppPermissions.Devices.Revoke)]
    public async Task<IActionResult> Release(ReleaseOfflineCacheRequest request)
    {
        await sender.Send(new ReleaseOfflineCacheCommand(
            request.LeaseId, request.LeaseToken, request.Force, request.Reason));
        return Ok();
    }

    [HttpPost("heartbeat")]
    [HasPermission(AppPermissions.Sales.Create, AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<OfflineHeartbeatDto>> Heartbeat(OfflineHeartbeatRequest request) =>
        Ok(await sender.Send(new HeartbeatOfflineCacheCommand(
            request.LeaseId, request.Epoch, request.LeaseToken, request.PendingCount)));

    [HttpPost("hub-attestation")]
    [HasPermission(AppPermissions.Sales.Create, AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<HubAttestationDto>> GetHubAttestation(HubAttestationRequest request) =>
        Ok(await sender.Send(new GetHubAttestationQuery(request.PublicKey)));

    [HttpGet("snapshot")]
    [HasPermission(AppPermissions.Sales.Create, AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<OfflineSnapshotDto>> GetSnapshot(
        [FromQuery] long leaseId,
        [FromQuery] long epoch,
        [FromHeader(Name = "X-Offline-Lease-Token")] string leaseToken,
        [FromQuery] string? sections = null,
        [FromQuery] DateTime? since = null)
    {
        var parts = string.IsNullOrWhiteSpace(sections)
            ? null
            : sections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var snapshot = await sender.Send(new GetOfflineSnapshotQuery(leaseId, epoch, leaseToken,
            parts is null || parts.Contains("sales", StringComparer.OrdinalIgnoreCase),
            parts is null || parts.Contains("payments", StringComparer.OrdinalIgnoreCase),
            parts is null || parts.Contains("supplies", StringComparer.OrdinalIgnoreCase),
            since));
        return Ok(snapshot);
    }

    [HttpPost("sync/batches")]
    [HasPermission(AppPermissions.Sales.Create, AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<OfflineSyncBatchResult>> Sync(OfflineSyncBatchRequest request) =>
        Ok(await sender.Send(new ProcessOfflineSyncBatch(
            request.LeaseId, request.Epoch, request.LeaseToken, request.Events)));

    [HttpPost("sync/skip")]
    [HasPermission(AppPermissions.Sales.Create, AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<OfflineSyncEventResult>> Skip(OfflineSyncSkipRequest request) =>
        Ok(await sender.Send(new SkipOfflineSyncEventCommand(
            request.LeaseId, request.Epoch, request.LeaseToken, request.Event, request.Reason)));

    [HttpPost("sync/import")]
    [HasPermission(AppPermissions.Devices.Revoke)]
    public async Task<ActionResult<OfflineSyncBatchResult>> Import(OfflineSyncImportRequest request) =>
        Ok(await sender.Send(new ImportOfflineSyncCommand(
            request.LeaseId, request.Epoch, request.LeaseToken, request.Events, request.SkipRejected,
            request.SkipEventIds)));
}
