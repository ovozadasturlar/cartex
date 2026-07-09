using Cartex.Application.Loyalty.Commands;
using Cartex.Application.Loyalty.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Loyalty;
using Cartex.Application.Common.Messaging;
using LoyaltyProgramDto = Cartex.Application.Loyalty.Queries.LoyaltyProgramDto;
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
    public async Task<ActionResult<LoyaltyProgramDto>> GetProgram()
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
    public async Task<ActionResult<long>> CreateRule(CreateCashbackRuleRequest request)
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
    [HttpGet("discounts")]
    [HasPermission(AppPermissions.Loyalty.View)]
    public async Task<ActionResult<IReadOnlyCollection<DiscountRuleDto>>> GetDiscountRules()
    {
        var result = await sender.Send(new GetDiscountRulesQuery());
        return Ok(result);
    }

    [HttpPost("discounts")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<ActionResult<long>> SaveDiscountRule(SaveDiscountRuleCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpDelete("discounts/{id:long}")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> DeleteDiscountRule(long id)
    {
        await sender.Send(new DeleteDiscountRuleCommand(id));
        return NoContent();
    }

    [HttpPost("discount-preview")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<PreviewDiscountResult>> PreviewDiscount(PreviewDiscountQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}