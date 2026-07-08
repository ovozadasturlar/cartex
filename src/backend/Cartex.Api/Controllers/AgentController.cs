using Cartex.Application.Agents.Queries;
using Cartex.Application.Common.Messaging;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
[RequiresFeature(FeatureCatalog.Agents)]
public class AgentController(ISender sender) : ControllerBase
{
    [HttpGet("bootstrap")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<AgentBootstrapDto>> Bootstrap() =>
        Ok(await sender.Send(new GetAgentBootstrapQuery()));
}
