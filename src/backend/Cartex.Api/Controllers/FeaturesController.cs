using Cartex.Application.Features.Commands;
using Cartex.Application.Features.Queries;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Features;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeaturesController(ISender sender) : ControllerBase
{
    [HttpGet("enabled")]
    public async Task<IActionResult> GetEnabled([FromServices] IApplicationDbContext db, [FromServices] IFeatureStateProvider features, CancellationToken cancellationToken)
    {
        var codes = await db.Features.Select(f => f.Code).ToListAsync(cancellationToken);
        var enabled = new List<string>();
        foreach (var code in codes)
            if (await features.IsEnabledAsync(code, cancellationToken))
                enabled.Add(code);
        return Ok(enabled);
    }

    [HttpGet]
    [HasPermission(AppPermissions.Features.Manage)]
    public async Task<IActionResult> GetFeatures() =>
        Ok(await sender.Send(new GetFeaturesQuery()));

    [HttpPut("{code}")]
    [HasPermission(AppPermissions.Features.Manage)]
    public async Task<IActionResult> SetFeature(string code, [FromBody] SetFeatureRequest body)
    {
        await sender.Send(new SetFeatureCommand(code, body.IsEnabled));
        return NoContent();
    }
}
