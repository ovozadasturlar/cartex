using Cartex.Application.StockTransfers.Commands;
using Cartex.Application.StockTransfers.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
[Authorize]
[RequiresFeature(FeatureCatalog.StockTransfers)]
public class StockTransfersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.StockTransfers.View)]
    public async Task<ActionResult<IReadOnlyCollection<StockTransferDto>>> GetStockTransfers([FromQuery] GetStockTransfersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.StockTransfers.View)]
    public async Task<ActionResult<StockTransfersTotalsDto>> GetTotals([FromQuery] GetStockTransfersTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.StockTransfers.Manage)]
    public async Task<ActionResult<long>> CreateStockTransfer(CreateStockTransferCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}/receive")]
    [HasPermission(AppPermissions.StockTransfers.Manage)]
    public async Task<IActionResult> ReceiveStockTransfer(long id)
    {
        await sender.Send(new ReceiveStockTransferCommand(id));
        return NoContent();
    }
}
