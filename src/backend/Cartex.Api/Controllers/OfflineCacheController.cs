using Cartex.Application.Common.Messaging;
using Cartex.Application.OfflineCache.Commands;
using Cartex.Application.OfflineCache.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/offline-cache")]
[Authorize]
[RequiresFeature(FeatureCatalog.OfflineCache)]
public class OfflineCacheController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Devices.Manage)]
    public async Task<ActionResult<OfflineCacheStateDto>> GetState()
    {
        var state = await sender.Send(new GetOfflineCacheStateQuery());
        return Ok(state);
    }

    [HttpPost("claim")]
    [HasPermission(AppPermissions.Devices.Manage)]
    public async Task<IActionResult> Claim(ClaimOfflineCacheCommand command)
    {
        await sender.Send(command);
        return Ok();
    }

    [HttpPost("release")]
    [HasPermission(AppPermissions.Devices.Manage)]
    public async Task<IActionResult> Release()
    {
        await sender.Send(new ReleaseOfflineCacheCommand());
        return Ok();
    }

    [HttpGet("snapshot")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<OfflineSnapshotDto>> GetSnapshot([FromQuery] long warehouseId, [FromQuery] string deviceId)
    {
        var snapshot = await sender.Send(new GetOfflineSnapshotQuery(warehouseId, deviceId));
        return Ok(snapshot);
    }
}
