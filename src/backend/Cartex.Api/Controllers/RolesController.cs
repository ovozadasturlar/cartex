using Cartex.Application.Roles.Commands;
using Cartex.Application.Roles.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SetRoleActiveRequest = Cartex.Shared.Models.Roles.SetRoleActiveRequest;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Roles.View)]
    public async Task<ActionResult<IReadOnlyCollection<RoleDto>>> GetRoles([FromQuery] GetRolesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Roles.Create)]
    public async Task<ActionResult<long>> CreateRole(CreateRoleCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Roles.Edit)]
    public async Task<IActionResult> UpdateRole(long id, UpdateRoleCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPut("{id:long}/permissions")]
    [HasPermission(AppPermissions.Roles.AssignPermissions)]
    public async Task<IActionResult> AssignPermissions(long id, AssignPermissionsCommand command)
    {
        await sender.Send(command with { RoleId = id });
        return NoContent();
    }

    [HttpPut("{id:long}/active")]
    [HasPermission(AppPermissions.Roles.Edit)]
    public async Task<IActionResult> SetActive(long id, SetRoleActiveRequest request)
    {
        await sender.Send(new SetRoleActiveCommand(id, request.IsActive));
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Roles.Delete)]
    public async Task<IActionResult> DeleteRole(long id)
    {
        await sender.Send(new DeleteRoleCommand(id));
        return NoContent();
    }
}
