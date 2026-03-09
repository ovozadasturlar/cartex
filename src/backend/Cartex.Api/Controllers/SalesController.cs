using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("sales.view")]
    public async Task<IActionResult> GetSales([FromQuery] GetSalesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("sales.create")]
    public async Task<IActionResult> CreateSale(CreateSaleCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
