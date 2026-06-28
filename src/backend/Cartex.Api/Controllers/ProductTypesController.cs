using Cartex.Application.ProductTypes.Commands;
using Cartex.Application.ProductTypes.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/product-types")]
[Authorize]
public class ProductTypesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("products.view")]
    public async Task<IActionResult> GetProductTypes()
    {
        var result = await sender.Send(new GetProductTypesQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("products.manage")]
    public async Task<IActionResult> CreateProductType(CreateProductTypeCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
