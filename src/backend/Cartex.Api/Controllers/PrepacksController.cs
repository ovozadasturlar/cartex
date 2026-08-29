using Cartex.Application.Common.Messaging;
using Cartex.Application.Prepacks.Commands;
using Cartex.Application.Prepacks.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Prepacks;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Prepack)]
public class PrepacksController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Sales.Prepack)]
    public async Task<ActionResult<IReadOnlyList<PrepackDto>>> GetPrepacks([FromQuery] long warehouseId) =>
        Ok(await sender.Send(new GetPrepacksQuery(warehouseId)));

    [HttpGet("by-code")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<PrepackLookupDto>> GetByCode([FromQuery] string code, [FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetPrepackByCodeQuery(code, warehouseId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Sales.Prepack)]
    public async Task<ActionResult<List<PrepackLabelDto>>> CreatePrepacks(CreatePrepacksCommand command) =>
        Ok(await sender.Send(command));

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Sales.Prepack)]
    public async Task<IActionResult> CancelPrepack(long id)
    {
        await sender.Send(new CancelPrepackCommand(id));
        return NoContent();
    }
}
