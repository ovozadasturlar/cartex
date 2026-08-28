using Cartex.Application.Catalog.Queries;
using Cartex.Application.Common.Messaging;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CatalogController(ISender sender) : ControllerBase
{
    [HttpGet("by-barcode")]
    [HasPermission(AppPermissions.Products.View, AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit)]
    public async Task<ActionResult<CatalogProductDto>> ByBarcode([FromQuery] string code)
    {
        var result = await sender.Send(new GetCatalogReferenceByBarcodeQuery(code));
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("search")]
    [HasPermission(AppPermissions.Products.View, AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit)]
    public async Task<ActionResult<IReadOnlyList<CatalogProductDto>>> Search(
        [FromQuery] string? q, [FromQuery] int limit = 10) =>
        Ok(await sender.Send(new SearchCatalogReferenceQuery(q, limit)));
}
