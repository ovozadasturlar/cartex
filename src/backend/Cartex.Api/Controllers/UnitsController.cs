using Cartex.Application.Units.Commands;
using Cartex.Application.Units.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UnitsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Units.View)]
    public async Task<ActionResult<IReadOnlyCollection<UnitDto>>> GetUnits([FromQuery] GetUnitsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Units.Create)]
    public async Task<ActionResult<long>> CreateUnit(CreateUnitCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Units.Edit)]
    public async Task<IActionResult> UpdateUnit(long id, UpdateUnitCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPut("{id:long}/state")]
    [HasPermission(AppPermissions.Units.Toggle)]
    public async Task<IActionResult> SetUnitState(long id, SetUnitStateCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }
}
