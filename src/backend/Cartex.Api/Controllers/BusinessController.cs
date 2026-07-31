using Cartex.Application.Business.Commands;
using Cartex.Application.Business.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BusinessController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BusinessDto>> Get() =>
        Ok(await sender.Send(new GetBusinessQuery()));

    [HttpPut]
    [HasPermission(AppPermissions.Business.Edit)]
    public async Task<IActionResult> Update(UpdateBusinessCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpPost("complete-onboarding")]
    [HasPermission(AppPermissions.Business.Edit)]
    public async Task<IActionResult> CompleteOnboarding(CompleteOnboardingCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
