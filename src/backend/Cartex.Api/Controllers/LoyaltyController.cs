using Cartex.Application.Loyalty.Commands;
using Cartex.Application.Loyalty.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Loyalty;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Loyalty)]
public class LoyaltyController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Loyalty.View)]
    public async Task<IActionResult> GetProgram()
    {
        var result = await sender.Send(new GetLoyaltyProgramQuery());
        return Ok(result);
    }

    [HttpPut]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> UpdateProgram(UpdateLoyaltyProgramCommand command)
    {
        await sender.Send(command);
        return Ok();
    }

    [HttpPost("rules")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> CreateRule(CreateCashbackRuleRequest request)
    {
        var id = await sender.Send(new CreateCashbackRuleCommand(
            Enum.Parse<CashbackScope>(request.Scope, true), request.TargetId,
            Enum.Parse<CashbackMethod>(request.Method, true), request.Value, request.Priority, request.ExcludeFromTotalPercent));
        return Ok(id);
    }

    [HttpPut("rules/{id:long}")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> UpdateRule(long id, UpdateCashbackRuleRequest request)
    {
        await sender.Send(new UpdateCashbackRuleCommand(
            id, Enum.Parse<CashbackScope>(request.Scope, true), request.TargetId,
            Enum.Parse<CashbackMethod>(request.Method, true), request.Value, request.Priority, request.ExcludeFromTotalPercent));
        return NoContent();
    }

    [HttpDelete("rules/{id:long}")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> DeleteRule(long id)
    {
        await sender.Send(new DeleteCashbackRuleCommand(id));
        return NoContent();
    }
}
