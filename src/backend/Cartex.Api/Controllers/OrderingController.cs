using Cartex.Application.Ordering.Commands;
using Cartex.Application.Ordering.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Ordering;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/ordering")]
[Authorize]
[RequiresFeature(FeatureCatalog.Ordering)]
public class OrderingController(ISender sender) : ControllerBase
{
    [HttpGet("carts")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<IActionResult> GetCarts([FromQuery] string? status = null)
    {
        var result = await sender.Send(new GetCartsQuery(status));
        return Ok(result);
    }

    [HttpPut("carts/{code}/status")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> UpdateStatus(string code, UpdateCartStatusRequest request)
    {
        if (!Enum.TryParse<Cartex.Domain.Enums.CartStatus>(request.Status, true, out var status))
            return BadRequest();
        await sender.Send(new UpdateCartStatusCommand(code, status));
        return NoContent();
    }

    [HttpPost("carts")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> Submit(SubmitCartCommand command)
    {
        var code = await sender.Send(command);
        return Ok(code);
    }

    [HttpGet("carts/{code}")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var cart = await sender.Send(new GetCartByCodeQuery(code));
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpPost("carts/{code}/checkout")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> Checkout(string code, CheckoutCartRequest request)
    {
        var saleId = await sender.Send(new CheckoutCartCommand(code, request.PaidCash, request.PaidCard, request.PaidBonus));
        return Ok(saleId);
    }
}
