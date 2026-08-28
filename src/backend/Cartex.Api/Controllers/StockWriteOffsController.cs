using Cartex.Application.Common.Messaging;
using Cartex.Application.StockWriteOffs.Commands;
using Cartex.Application.StockWriteOffs.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Stocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/stock-write-offs")]
[Authorize]
public class StockWriteOffsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission(AppPermissions.Stocks.WriteOff)]
    public async Task<ActionResult<StockWriteOffCreatedDto>> Create(CreateStockWriteOffCommand command) =>
        Ok(await sender.Send(command));

    [HttpPost("{id:long}/reverse")]
    [HasPermission(AppPermissions.Stocks.WriteOff)]
    public async Task<ActionResult<StockWriteOffCreatedDto>> Reverse(long id, ReverseStockWriteOffCommand command) =>
        Ok(await sender.Send(command with { DocumentId = id }));

    [HttpGet]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyCollection<StockWriteOffDto>>> Get(
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] long? warehouseId = null,
        [FromQuery] StockWriteOffReason? reason = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await sender.Send(new GetStockWriteOffsQuery(from, to, warehouseId, reason, page, pageSize)));

    [HttpGet("balances")]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyList<WriteOffBalanceDto>>> GetBalances(
        [FromQuery] long? warehouseId = null, [FromQuery] long? variantId = null) =>
        Ok(await sender.Send(new GetWriteOffBalancesQuery(warehouseId, variantId)));

    [HttpGet("batches")]
    [HasPermission(AppPermissions.Stocks.WriteOff)]
    public async Task<ActionResult<IReadOnlyList<WriteOffBatchDto>>> GetBatches(
        [FromQuery] long warehouseId, [FromQuery] long variantId) =>
        Ok(await sender.Send(new GetWriteOffBatchesQuery(warehouseId, variantId)));
}
