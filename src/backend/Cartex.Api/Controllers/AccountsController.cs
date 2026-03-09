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
    public async Task<IActionResult> GetAccounts([FromQuery] GetAccountsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
