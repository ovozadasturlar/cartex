using Cartex.Application.Common.Messaging;
using Cartex.Application.ProductReference.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/product-reference")]
[Authorize]
public sealed class ProductReferenceController(ISender sender) : ControllerBase
{
    [HttpGet("by-barcode/{code}")]
    [HasPermission(AppPermissions.Products.View, AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit)]
    public async Task<ActionResult<ProductReferenceDto>> ByBarcode(string code)
    {
        var result = await sender.Send(new GetProductReferenceByBarcodeQuery(code));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyList<ProductReferenceDto>>> Search([FromQuery] string? search, [FromQuery] int take = 20) =>
        Ok(await sender.Send(new SearchProductReferenceQuery(search, take)));
}
