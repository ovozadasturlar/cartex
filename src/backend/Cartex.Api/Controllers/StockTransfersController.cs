using Cartex.Application.StockTransfers.Commands;
using Cartex.Application.StockTransfers.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
[Authorize]
public class StockTransfersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("stock_transfers.view")]
    public async Task<IActionResult> GetStockTransfers([FromQuery] long? warehouseId)
    {
        var result = await sender.Send(new GetStockTransfersQuery(warehouseId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("stock_transfers.manage")]
    public async Task<IActionResult> CreateStockTransfer(CreateStockTransferCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}/receive")]
    [HasPermission("stock_transfers.manage")]
    public async Task<IActionResult> ReceiveStockTransfer(long id)
    {
        await sender.Send(new ReceiveStockTransferCommand(id));
        return NoContent();
    }
}
