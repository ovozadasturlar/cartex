using Cartex.Application.Branches.Commands;
using Cartex.Application.Branches.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("branches.view")]
    public async Task<IActionResult> GetBranches([FromQuery] GetBranchesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("branches.manage")]
    public async Task<IActionResult> CreateBranch(CreateBranchCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission("branches.manage")]
    public async Task<IActionResult> UpdateBranch(long id, UpdateBranchCommand command)
    {
        if (id != command.Id) return BadRequest();
        await sender.Send(command);
        return NoContent();
    }
}
