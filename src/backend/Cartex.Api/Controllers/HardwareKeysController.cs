using Cartex.Application.Auth.Commands;
using Cartex.Application.Auth.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SetHardwareKeyEnabledRequest = Cartex.Shared.Models.Auth.SetHardwareKeyEnabledRequest;
using Cartex.Shared.Models.Auth;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/hardware-keys")]
[Authorize]
public class HardwareKeysController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Keys.View)]
    public async Task<ActionResult<IReadOnlyList<HardwareKeyDto>>> GetKeys() =>
        Ok(await sender.Send(new GetHardwareKeysQuery()));

    [HttpPost]
    [HasPermission(AppPermissions.Keys.Create)]
    public async Task<ActionResult<HardwareKeyResult>> Generate(GenerateHardwareKeyCommand command) =>
        Ok(await sender.Send(command));

    [HttpPut("{id:long}/enabled")]
    [HasPermission(AppPermissions.Keys.Edit)]
    public async Task<IActionResult> SetEnabled(long id, [FromBody] SetHardwareKeyEnabledRequest request)
    {
        await sender.Send(new SetHardwareKeyEnabledCommand(id, request.Enabled));
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Keys.Revoke)]
    public async Task<IActionResult> Revoke(long id)
    {
        await sender.Send(new RevokeHardwareKeyCommand(id));
        return NoContent();
    }
}
