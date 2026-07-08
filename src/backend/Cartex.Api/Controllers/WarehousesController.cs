using Cartex.Application.Warehouses.Commands;
using Cartex.Application.Warehouses.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WarehousesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Warehouses.View)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseDto>>> GetWarehouses([FromQuery] GetWarehousesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Warehouses.Manage)]
    public async Task<ActionResult<long>> CreateWarehouse(CreateWarehouseCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Warehouses.Manage)]
    public async Task<IActionResult> UpdateWarehouse(long id, UpdateWarehouseCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }
}
