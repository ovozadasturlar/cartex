using Cartex.Application.ProductTypes.Commands;
using Cartex.Application.ProductTypes.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Products;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/product-types")]
[Authorize]
public class ProductTypesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.ProductTypes.View)]
    public async Task<ActionResult<IReadOnlyCollection<ProductTypeDto>>> GetProductTypes()
    {
        var result = await sender.Send(new GetProductTypesQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductTypes.Create)]
    public async Task<ActionResult<long>> CreateProductType(CreateProductTypeCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.ProductTypes.Edit)]
    public async Task<IActionResult> UpdateProductType(long id, UpdateProductTypeCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }
}
