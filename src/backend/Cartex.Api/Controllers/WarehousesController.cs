using Cartex.Application.Warehouses.Commands;
using Cartex.Application.Warehouses.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WarehousesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("warehouses.view")]
    public async Task<IActionResult> GetWarehouses([FromQuery] long? shopId)
    {
        var result = await sender.Send(new GetWarehousesQuery(shopId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("warehouses.manage")]
    public async Task<IActionResult> CreateWarehouse(CreateWarehouseCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
