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
    public async Task<IActionResult> GetSales(
        [FromQuery] long? warehouseId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var result = await sender.Send(new GetSalesQuery(warehouseId, fromDate, toDate));
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
