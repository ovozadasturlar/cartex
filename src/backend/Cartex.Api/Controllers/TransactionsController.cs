using Cartex.Application.Transactions.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("transactions.view")]
    public async Task<IActionResult> GetTransactions([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await sender.Send(new GetTransactionsQuery(fromDate, toDate));
        return Ok(result);
    }
}
