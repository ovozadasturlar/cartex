using Cartex.Application.Products.Commands;
using Cartex.Application.Products.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("products.view")]
    public async Task<IActionResult> GetProducts([FromQuery] GetProductsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("by-barcode")]
    [HasPermission("sales.create")]
    public async Task<IActionResult> GetByBarcode([FromQuery] string code, [FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetProductByBarcodeQuery(code, warehouseId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission("products.manage")]
    public async Task<IActionResult> CreateProduct(CreateProductCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission("products.manage")]
    public async Task<IActionResult> UpdateProduct(long id, UpdateProductCommand command)
    {
        if (id != command.Id) return BadRequest();
        await sender.Send(command);
        return NoContent();
    }
}
