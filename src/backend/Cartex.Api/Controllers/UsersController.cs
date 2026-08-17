using Cartex.Application.Users.Commands;
using Cartex.Application.Users.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Users;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Users.View)]
    public async Task<ActionResult<IReadOnlyCollection<UserDto>>> GetUsers([FromQuery] GetUsersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Users.Create)]
    public async Task<ActionResult<long>> CreateUser(CreateUserCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Users.Edit)]
    public async Task<IActionResult> UpdateUser(long id, UpdateUserCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Users.Delete)]
    public async Task<IActionResult> DeleteUser(long id)
    {
        await sender.Send(new DeleteUserCommand(id));
        return NoContent();
    }

    [HttpPatch("{id:long}/username")]
    [HasPermission(AppPermissions.Wildcard)]
    public async Task<IActionResult> ChangeUsername(long id, [FromBody] ChangeUsernameRequest request)
    {
        await sender.Send(new ChangeUsernameCommand(id, request.NewUsername));
        return NoContent();
    }
}

public record ChangeUsernameRequest(string NewUsername);
