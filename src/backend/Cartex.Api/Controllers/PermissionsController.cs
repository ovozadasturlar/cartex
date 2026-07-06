using Cartex.Application.Permissions.Commands;
using Cartex.Application.Permissions.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Roles.View)]
    public async Task<IActionResult> GetPermissions([FromQuery] GetPermissionsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPut("{id:long}/toggle")]
    [HasPermission(AppPermissions.Permissions.Govern)]
    public async Task<IActionResult> TogglePermission(long id, [FromBody] TogglePermissionRequest request)
    {
        await sender.Send(new TogglePermissionCommand(id, request.IsEnabled));
        return NoContent();
    }
}

public record TogglePermissionRequest(bool IsEnabled);
