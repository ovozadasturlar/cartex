using Cartex.Application.Features.Commands;
using Cartex.Application.Features.Queries;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Features;
using Cartex.Application.Common.Messaging;
using FeatureDto = Cartex.Application.Features.Queries.FeatureDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeaturesController(ISender sender) : ControllerBase
{
    [HttpGet("enabled")]
    public async Task<ActionResult<List<string>>> GetEnabled([FromServices] IApplicationDbContext db, [FromServices] IFeatureStateProvider features, CancellationToken cancellationToken)
    {
        var codes = await db.Features.Select(f => f.Code).ToListAsync(cancellationToken);
        var enabled = new List<string>();
        foreach (var code in codes)
            if (await features.IsEnabledAsync(code, cancellationToken))
                enabled.Add(code);
        return Ok(enabled);
    }

    // The owner's own view: only the modules a shop may switch, and only its own switch.
    // The vendor screen above stays where it is, under the developer section.
    [HttpGet("modules")]
    [HasPermission(AppPermissions.Business.Edit)]
    public async Task<ActionResult<IReadOnlyList<OwnerModuleDto>>> GetModules() =>
        Ok(await sender.Send(new GetOwnerModulesQuery()));

    [HttpPut("modules/{code}")]
    [HasPermission(AppPermissions.Business.Edit)]
    public async Task<IActionResult> SetModule(string code, [FromBody] SetFeatureRequest body)
    {
        await sender.Send(new SetOwnerModuleCommand(code, body.IsEnabled));
        return NoContent();
    }

    [HttpGet]
    [HasPermission(AppPermissions.Features.View)]
    public async Task<ActionResult<IReadOnlyList<FeatureDto>>> GetFeatures() =>
        Ok(await sender.Send(new GetFeaturesQuery()));

    [HttpPut("{code}")]
    [HasPermission(AppPermissions.Features.Edit)]
    public async Task<IActionResult> SetFeature(string code, [FromBody] SetFeatureRequest body)
    {
        await sender.Send(new SetFeatureCommand(code, body.IsEnabled));
        return NoContent();
    }
}
