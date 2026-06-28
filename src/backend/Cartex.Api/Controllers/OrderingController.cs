using Cartex.Application.Ordering.Commands;
using Cartex.Application.Ordering.Queries;
using Cartex.Auth.Authorization;
using Cartex.Shared.Models.Ordering;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/ordering")]
[Authorize]
public class OrderingController(ISender sender, IOptions<FeatureOptions> features) : ControllerBase
{
    private bool Enabled => features.Value.Ordering;

    [HttpPost("carts")]
    [HasPermission("sales.create")]
    public async Task<IActionResult> Submit(SubmitCartCommand command)
    {
        if (!Enabled) return NotFound();
        var code = await sender.Send(command);
        return Ok(code);
    }

    [HttpGet("carts/{code}")]
    [HasPermission("sales.create")]
    public async Task<IActionResult> GetByCode(string code)
    {
        if (!Enabled) return NotFound();
        var cart = await sender.Send(new GetCartByCodeQuery(code));
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpPost("carts/{code}/checkout")]
    [HasPermission("sales.create")]
    public async Task<IActionResult> Checkout(string code, CheckoutCartRequest request)
    {
        if (!Enabled) return NotFound();
        var saleId = await sender.Send(new CheckoutCartCommand(code, request.PaidCash, request.PaidCard, request.PaidBonus));
        return Ok(saleId);
    }
}
