using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("supplies.view")]
    public async Task<IActionResult> GetSupplies([FromQuery] long? warehouseId)
    {
        var result = await sender.Send(new GetSuppliesQuery(warehouseId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("supplies.manage")]
    public async Task<IActionResult> CreateSupply(CreateSupplyCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
