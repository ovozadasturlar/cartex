using Cartex.Application.Auth.Commands;
using Cartex.Application.Auth.Queries;
using Cartex.Application.Common.Messaging;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginCommand command) =>
        Ok(await sender.Send(command));

    [AllowAnonymous]
    [HttpPost("login-with-key")]
    public async Task<ActionResult<LoginResponse>> LoginWithKey(LoginWithKeyCommand command) =>
        Ok(await sender.Send(command));

    [AllowAnonymous]
    [HttpGet("qr/enabled")]
    public async Task<ActionResult<bool>> QrLoginEnabled() =>
        Ok(await sender.Send(new GetQrLoginEnabledQuery()));

    [AllowAnonymous]
    [HttpPost("qr/start")]
    public async Task<ActionResult<QrLoginStartResponse>> StartQrLogin() =>
        Ok(await sender.Send(new StartQrLoginCommand()));

    [Authorize]
    [HttpPost("qr/approve")]
    public async Task<IActionResult> ApproveQrLogin(ApproveQrLoginCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("public")]
    [HttpPost("qr/poll")]
    public async Task<ActionResult<LoginResponse>> PollQrLogin(PollQrLoginCommand command)
    {
        var result = await sender.Send(command);
        return result is null ? NoContent() : Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenCommand command) =>
        Ok(await sender.Send(command));

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("sessions")]
    [HasPermission(AppPermissions.Devices.Manage)]
    public async Task<ActionResult<IReadOnlyList<DeviceSessionDto>>> Sessions([FromQuery] bool all = false) =>
        Ok(await sender.Send(new GetSessionsQuery(all)));

    [HttpDelete("sessions/{id:long}")]
    [HasPermission(AppPermissions.Devices.Manage)]
    public async Task<IActionResult> RevokeSession(long id)
    {
        await sender.Send(new RevokeSessionCommand(id));
        return NoContent();
    }
}
