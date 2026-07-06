using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Supplies)]
public class SuppliesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Supplies.View)]
    public async Task<IActionResult> GetSupplies([FromQuery] GetSuppliesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Supplies.View)]
    public async Task<IActionResult> GetTotals([FromQuery] GetSuppliesTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Supplies.Manage)]
    public async Task<IActionResult> CreateSupply(CreateSupplyCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
