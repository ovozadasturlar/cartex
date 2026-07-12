using Cartex.Application.Ordering.Commands;
using Cartex.Application.Ordering.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Ordering;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CartDto = Cartex.Application.Ordering.Queries.CartDto;
using CartListDto = Cartex.Application.Ordering.Queries.CartListDto;
using CartLoadItemDto = Cartex.Application.Ordering.Queries.CartLoadItemDto;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/ordering")]
[Authorize]
[RequiresFeature(FeatureCatalog.Ordering, FeatureCatalog.Store)]
public class OrderingController(ISender sender) : ControllerBase
{
    [HttpGet("carts")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<CartListDto>>> GetCarts([FromQuery] string? status = null, [FromQuery] long? warehouseId = null)
    {
        var result = await sender.Send(new GetCartsQuery(status, warehouseId));
        return Ok(result);
    }

    [HttpGet("load")]
    [HasPermission(AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<CartLoadItemDto>>> GetLoad([FromQuery] long? warehouseId = null, [FromQuery] string? status = null)
    {
        var result = await sender.Send(new GetCartLoadQuery(warehouseId, status));
        return Ok(result);
    }

    [HttpPut("carts/{code}/status")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<IActionResult> UpdateStatus(string code, UpdateCartStatusRequest request)
    {
        if (!Enum.TryParse<Cartex.Domain.Enums.CartStatus>(request.Status, true, out var status))
            return BadRequest();
        await sender.Send(new UpdateCartStatusCommand(code, status));
        return NoContent();
    }

    [HttpPost("carts")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<ActionResult<string>> Submit(SubmitCartCommand command)
    {
        var code = await sender.Send(command);
        return Ok(code);
    }

    [HttpGet("carts/{code}")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<ActionResult<CartDto>> GetByCode(string code)
    {
        var cart = await sender.Send(new GetCartByCodeQuery(code));
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpPost("carts/{code}/checkout")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<ActionResult<long>> Checkout(string code, CheckoutCartRequest request)
    {
        var saleId = await sender.Send(new CheckoutCartCommand(code, request.PaidCash, request.PaidCard, request.PaidBonus, request.IdempotencyKey));
        return Ok(saleId);
    }
}
