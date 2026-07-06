using Cartex.Application.Auth.Commands;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/hardware-keys")]
[Authorize]
public class HardwareKeysController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission(AppPermissions.Keys.Manage)]
    public async Task<IActionResult> Generate(GenerateHardwareKeyCommand command) =>
        Ok(await sender.Send(command));
}
