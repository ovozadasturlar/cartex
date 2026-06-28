using Cartex.Application.Loyalty.Commands;
using Cartex.Application.Loyalty.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LoyaltyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("loyalty.view")]
    public async Task<IActionResult> GetProgram()
    {
        var result = await sender.Send(new GetLoyaltyProgramQuery());
        return Ok(result);
    }

    [HttpPut]
    [HasPermission("loyalty.manage")]
    public async Task<IActionResult> UpdateProgram(UpdateLoyaltyProgramCommand command)
    {
        await sender.Send(command);
        return Ok();
    }
}
