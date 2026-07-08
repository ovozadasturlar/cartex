using Cartex.Application.Common.Interfaces;
using Cartex.Application.Products.Commands;
using Cartex.Application.Products.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<ProductDto>>> GetProducts([FromQuery] GetProductsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<ProductsTotalsDto>> GetTotals([FromQuery] GetProductsTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("by-barcode")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<ProductLookupDto>> GetByBarcode([FromQuery] string code, [FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetProductByBarcodeQuery(code, warehouseId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("catalog-lookup")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<ActionResult<ProductCatalogInfo>> CatalogLookup([FromQuery] string barcode)
    {
        var result = await sender.Send(new GetCatalogInfoByBarcodeQuery(barcode));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("variants/{id:long}/price-info")]
    [HasPermission(AppPermissions.Supplies.Manage)]
    public async Task<ActionResult<VariantPriceInfoDto>> GetVariantPriceInfo(long id, [FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetVariantPriceInfoQuery(id, warehouseId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<ActionResult<long>> CreateProduct(CreateProductCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> UpdateProduct(long id, UpdateProductCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPost("price")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> SetPrice(SetProductPriceCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("{productId:long}/variants")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<VariantDto>>> GetVariants(long productId)
    {
        var result = await sender.Send(new GetProductVariantsQuery(productId));
        return Ok(result);
    }

    [HttpPost("{productId:long}/variants")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<ActionResult<long>> CreateVariant(long productId, CreateVariantCommand command)
    {
        var id = await sender.Send(command with { ProductId = productId });
        return Ok(id);
    }

    [HttpPut("variants/{id:long}")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> UpdateVariant(long id, UpdateVariantCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("variants/{id:long}")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> DeleteVariant(long id)
    {
        await sender.Send(new DeleteVariantCommand(id));
        return NoContent();
    }
}
