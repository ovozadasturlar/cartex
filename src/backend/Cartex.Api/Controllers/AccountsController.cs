using Cartex.Application.Accounts.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("accounts.view")]
    public async Task<IActionResult> GetAccounts([FromQuery] string? ownerType, [FromQuery] long? ownerId)
    {
        var result = await sender.Send(new GetAccountsQuery(ownerType, ownerId));
        return Ok(result);
    }
}
