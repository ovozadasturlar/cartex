using Cartex.Application.Auth.Commands;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command)
    {
        var result = await sender.Send(command);
        return Ok(result);
    }

    [HttpPost("login-with-key")]
    public async Task<IActionResult> LoginWithKey(LoginWithKeyCommand command)
    {
        var result = await sender.Send(command);
        return Ok(result);
    }
}
