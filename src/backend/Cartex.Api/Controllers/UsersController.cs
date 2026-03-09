using Cartex.Application.Users.Commands;
using Cartex.Application.Users.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("users.view")]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("users.manage")]
    public async Task<IActionResult> CreateUser(CreateUserCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> UpdateUser(long id, UpdateUserCommand command)
    {
        if (id != command.Id) return BadRequest();
        await sender.Send(command);
        return NoContent();
    }
}
