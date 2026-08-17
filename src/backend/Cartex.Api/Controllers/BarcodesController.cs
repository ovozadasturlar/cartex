using Cartex.Application.Barcodes.Commands;
using Cartex.Application.Barcodes.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Barcodes;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BarcodesController(ISender sender) : ControllerBase
{
    [HttpGet("by-variant/{variantId:long}")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<BarcodeDto>>> GetByVariant(long variantId)
    {
        var result = await sender.Send(new GetBarcodesByVariantQuery(variantId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Barcodes.Create)]
    public async Task<ActionResult<long>> CreateBarcode(CreateBarcodeCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Barcodes.Delete)]
    public async Task<IActionResult> DeleteBarcode(long id)
    {
        await sender.Send(new DeleteBarcodeCommand(id));
        return NoContent();
    }

    [HttpPost("generate/{variantId:long}")]
    [HasPermission(AppPermissions.Products.PrintBarcode)]
    public async Task<ActionResult<string>> Generate(long variantId, [FromQuery] decimal packQty = 1)
    {
        var code = await sender.Send(new GenerateBarcodeCommand(variantId, packQty));
        return Ok(code);
    }
}
