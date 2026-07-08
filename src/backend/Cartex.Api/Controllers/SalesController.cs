using Cartex.Application.Sales.Commands;
using Cartex.Application.Sales.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Sales;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CreateSaleResult = Cartex.Application.Sales.Commands.CreateSaleResult;
using SaleDto = Cartex.Application.Sales.Queries.SaleDto;
using SalesTotalsDto = Cartex.Application.Sales.Queries.SalesTotalsDto;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<SaleDto>>> GetSales([FromQuery] GetSalesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<SalesTotalsDto>> GetTotals([FromQuery] GetSalesTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<CreateSaleResult>> CreateSale(CreateSaleCommand command)
    {
        var result = await sender.Send(command);
        return Ok(result);
    }

    [HttpPost("{id:long}/return")]
    [HasPermission(AppPermissions.Sales.Return)]
    public async Task<IActionResult> ReturnSale(long id, [FromBody] ReturnSaleRequest request)
    {
        var lines = request.Lines.Select(l => new ReturnLineDto(l.SaleItemId, l.Quantity, l.Restock, l.Reason)).ToList();
        await sender.Send(new ReturnSaleCommand(id, lines));
        return NoContent();
    }
}
