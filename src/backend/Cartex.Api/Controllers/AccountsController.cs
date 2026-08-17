using Cartex.Application.Accounts.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Accounts;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Accounts)]
public class AccountsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Accounts.View)]
    public async Task<ActionResult<IReadOnlyCollection<AccountDto>>> GetAccounts([FromQuery] GetAccountsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Accounts.View)]
    public async Task<ActionResult<AccountsTotalsDto>> GetTotals([FromQuery] GetAccountsTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
