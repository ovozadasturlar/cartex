using Cartex.Application.Barcodes.Commands;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BarcodesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [HasPermission("products.manage")]
    public async Task<IActionResult> CreateBarcode(CreateBarcodeCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
