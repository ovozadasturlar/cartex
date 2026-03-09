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
    public async Task<IActionResult> GetStocks([FromQuery] GetStocksQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }
}
