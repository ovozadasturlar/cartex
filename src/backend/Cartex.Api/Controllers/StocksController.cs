using Cartex.Application.Stocks.Commands;
using Cartex.Application.Stocks.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StocksController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyCollection<StockDto>>> GetStocks([FromQuery] GetStocksQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("on-hand")]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<StockOnHandPageDto>> GetOnHand([FromQuery] long warehouseId, [FromQuery] long? categoryId = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool forSale = false)
    {
        var result = await sender.Send(new GetStockOnHandQuery(warehouseId, categoryId, search, page, pageSize, forSale));
        return Ok(result);
    }

    [HttpPost("on-hand/by-variants")]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyList<StockOnHandDto>>> GetOnHandByVariants(
        [FromQuery] long warehouseId, [FromBody] IReadOnlyList<long> variantIds)
    {
        var result = await sender.Send(new GetStockOnHandByVariantsQuery(warehouseId, variantIds));
        return Ok(result);
    }

    [HttpGet("expiring")]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyCollection<ExpiringStockDto>>> GetExpiring([FromQuery] int withinDays = 30)
    {
        var result = await sender.Send(new GetExpiringStocksQuery(withinDays));
        return Ok(result);
    }

    [HttpGet("low-stock")]
    [HasPermission(AppPermissions.Stocks.View)]
    public async Task<ActionResult<IReadOnlyCollection<LowStockDto>>> GetLowStock([FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetLowStockQuery(warehouseId));
        return Ok(result);
    }

    [HttpPost("adjust")]
    [HasPermission(AppPermissions.Stocks.Adjust)]
    public async Task<IActionResult> Adjust(AdjustStockCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }
}
