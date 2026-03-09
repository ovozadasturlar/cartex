using Cartex.Application.Shops.Commands;
using Cartex.Application.Shops.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShopsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("shops.view")]
    public async Task<IActionResult> GetShops([FromQuery] GetShopsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("shops.manage")]
    public async Task<IActionResult> CreateShop(CreateShopCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission("shops.manage")]
    public async Task<IActionResult> UpdateShop(long id, UpdateShopCommand command)
    {
        if (id != command.Id) return BadRequest();
        await sender.Send(command);
        return NoContent();
    }
}
