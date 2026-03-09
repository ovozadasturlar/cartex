using Cartex.Application.Units.Commands;
using Cartex.Application.Units.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UnitsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("products.view")]
    public async Task<IActionResult> GetUnits()
    {
        var result = await sender.Send(new GetUnitsQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("products.manage")]
    public async Task<IActionResult> CreateUnit(CreateUnitCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
