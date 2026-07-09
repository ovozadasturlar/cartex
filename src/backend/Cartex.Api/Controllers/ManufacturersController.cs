using Cartex.Application.Common.Messaging;
using Cartex.Application.Manufacturers;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ManufacturersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<ManufacturerDto>>> GetManufacturers()
    {
        var result = await sender.Send(new GetManufacturersQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<ActionResult<long>> CreateManufacturer(CreateManufacturerCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> UpdateManufacturer(long id, UpdateManufacturerCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> DeleteManufacturer(long id)
    {
        await sender.Send(new DeleteManufacturerCommand(id));
        return NoContent();
    }
}
