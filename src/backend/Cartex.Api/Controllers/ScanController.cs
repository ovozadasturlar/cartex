using Cartex.Application.Common.Messaging;
using Cartex.Application.Scan.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Scan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/scan")]
[Authorize]
public sealed class ScanController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<ActionResult<ScanResultDto>> Scan(
        [FromQuery] string code, [FromQuery] long warehouseId, [FromQuery] bool forSale = false) =>
        Ok(await sender.Send(new ScanQuery(code, warehouseId, forSale)));
}
