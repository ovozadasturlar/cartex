using Cartex.Application.Roles.Commands;
using Cartex.Application.Roles.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("roles.view")]
    public async Task<IActionResult> GetRoles()
    {
        var result = await sender.Send(new GetRolesQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> CreateRole(CreateRoleCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}/permissions")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> AssignPermissions(long id, AssignPermissionsCommand command)
    {
        if (id != command.RoleId) return BadRequest();
        await sender.Send(command);
        return NoContent();
    }
}
