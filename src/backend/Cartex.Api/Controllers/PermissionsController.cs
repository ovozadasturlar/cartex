using Cartex.Application.Permissions.Commands;
using Cartex.Application.Permissions.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("roles.view")]
    public async Task<IActionResult> GetPermissions([FromQuery] GetPermissionsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPut("{id:long}/toggle")]
    [HasPermission("roles.manage")]
    public async Task<IActionResult> TogglePermission(long id, [FromBody] TogglePermissionRequest request)
    {
        await sender.Send(new TogglePermissionCommand(id, request.IsEnabled));
        return NoContent();
    }
}

public record TogglePermissionRequest(bool IsEnabled);
