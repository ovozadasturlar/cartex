using Cartex.Application.Common.Interfaces;
using Cartex.Application.Licensing.Commands;
using Cartex.Application.Licensing.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicenseController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Features.Manage)]
    public async Task<ActionResult<LicenseStatus>> GetStatus() =>
        Ok(await sender.Send(new GetLicenseStatusQuery()));

    [HttpGet("options")]
    [HasPermission(AppPermissions.Features.Manage)]
    public async Task<ActionResult<LicenseOptions>> GetOptions() =>
        Ok(await sender.Send(new GetLicenseOptionsQuery()));

    [HttpPut]
    [HasPermission(AppPermissions.Features.Manage)]
    public async Task<IActionResult> Update(UpdateLicenseCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
