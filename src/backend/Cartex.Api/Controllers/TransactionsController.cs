using Cartex.Application.Transactions.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Transactions;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Accounts)]
public class TransactionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Transactions.View)]
    public async Task<ActionResult<IReadOnlyCollection<TransactionDto>>> GetTransactions([FromQuery] GetTransactionsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Transactions.View)]
    public async Task<ActionResult<TransactionsTotalsDto>> GetTotals([FromQuery] GetTransactionsTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
