using Cartex.Application.Common.Messaging;
using Cartex.Application.Store;
using Cartex.Application.Store.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/store/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class StoreAuthController(ISender sender) : ControllerBase
{
    [HttpPost("request-otp")]
    public async Task<ActionResult<RequestOtpResponse>> RequestOtp(RequestStoreOtpCommand command) =>
        Ok(await sender.Send(command));

    [HttpPost("verify")]
    public async Task<ActionResult<StoreLoginResponse>> Verify(VerifyStoreOtpCommand command) =>
        Ok(await sender.Send(command));

    [HttpPost("refresh")]
    public async Task<ActionResult<StoreLoginResponse>> Refresh(RefreshStoreTokenCommand command) =>
        Ok(await sender.Send(command));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(StoreLogoutCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
