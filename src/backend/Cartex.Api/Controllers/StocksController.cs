using Cartex.Application.Stocks.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StocksController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("stocks.view")]
    public async Task<IActionResult> GetStocks([FromQuery] long warehouseId, [FromQuery] string? search)
    {
        var result = await sender.Send(new GetStocksQuery(warehouseId, search));
        return Ok(result);
    }
}
