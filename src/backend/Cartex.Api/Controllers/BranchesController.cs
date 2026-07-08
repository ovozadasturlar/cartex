using Cartex.Application.Branches.Commands;
using Cartex.Application.Branches.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Branches.View)]
    public async Task<ActionResult<IReadOnlyCollection<BranchDto>>> GetBranches([FromQuery] GetBranchesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Branches.Manage)]
    public async Task<ActionResult<long>> CreateBranch(CreateBranchCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Branches.Manage)]
    public async Task<IActionResult> UpdateBranch(long id, UpdateBranchCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }
}
