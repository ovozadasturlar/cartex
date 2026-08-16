using Cartex.Application.Ordering.Commands;
using Cartex.Application.Ordering.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Ordering;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Application.Sales.Commands;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Application.Common.Participants;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/ordering")]
[Authorize]
[RequiresFeature(FeatureCatalog.Ordering, FeatureCatalog.Store)]
public class OrderingController(ISender sender) : ControllerBase
{
    [HttpGet("carts")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.View)]
    public async Task<ActionResult<IReadOnlyCollection<CartListDto>>> GetCarts([FromQuery] string? status = null, [FromQuery] long? warehouseId = null, [FromQuery] string? kind = null)
    {
        var result = await sender.Send(new GetCartsQuery(status, warehouseId, kind));
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
        await sender.Send(new UpdateCartStatusCommand(code, status, request.Reason));
        return NoContent();
    }

    [HttpPut("carts/{code}/items")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> UpdateItems(string code, UpdateCartItemsRequest request)
    {
        await sender.Send(new UpdateCartItemsCommand(code,
            request.Items.Select(x => new SubmitCartItemDto(x.VariantId, x.Quantity, x.UnitPrice)).ToList(),
            request.ExpectedVersion));
        return NoContent();
    }

    [HttpPut("carts/{code}")]
    [HasPermission(AppPermissions.Sales.Create)]
    public async Task<IActionResult> Update(string code, UpdateCartRequest request)
    {
        await sender.Send(new UpdateCartCommand(
            code,
            request.CustomerId,
            request.Items.Select(x => new SubmitCartItemDto(x.VariantId, x.Quantity, x.UnitPrice)).ToList(),
            request.Note,
            request.Participants?.Select(x => new ParticipantInput(x.RoleDefinitionId, x.PartyId)).ToList(),
            request.Payments?.Select(x => new SalePaymentDto(
                ParsePaymentMethod(x.Method), x.Currency, x.Amount)).ToList(),
            request.DebtCurrency,
            request.DebtDueDate,
            request.CreditAmount,
            request.UseCustomerAdvance,
            request.ExpectedVersion));
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
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create, AppPermissions.Sales.View)]
    public async Task<ActionResult<CartDto>> GetByCode(string code)
    {
        var cart = await sender.Send(new GetCartByCodeQuery(code));
        return cart is null ? NotFound() : Ok(cart);
    }

    [HttpPost("carts/{code}/checkout")]
    [HasPermission(AppPermissions.Sales.Checkout)]
    public async Task<ActionResult<long>> Checkout(string code, CheckoutCartRequest request)
    {
        var items = request.Items?.Select(x => new CheckoutCartItemDto(x.VariantId, x.Quantity, x.UnitPrice)).ToList();
        var payments = request.Payments?.Select(x => new SalePaymentDto(
            ParsePaymentMethod(x.Method), x.Currency, x.Amount)).ToList();
        var saleId = await sender.Send(new CheckoutCartCommand(code, request.PaidCash, request.PaidCard,
            request.PaidBonus, request.IdempotencyKey, items, payments, request.DebtCurrency,
            request.DebtDueDate, request.CreditAmount, request.UseCustomerAdvance,
            request.CustomerId, request.DiscountAmount, request.Note));
        return Ok(saleId);
    }

    [HttpPost("carts/{code}/requeue")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<ActionResult<RequeueCartResult>> Requeue(string code, RequeueCartRequest request) =>
        Ok(await sender.Send(new RequeueCartCommand(code, request.Note, request.IdempotencyKey)));

    private static PaymentMethod ParsePaymentMethod(string value) =>
        Enum.TryParse<PaymentMethod>(value, true, out var method)
            ? method
            : throw new BusinessRuleException($"To'lov turi noto'g'ri: {value}", "invalid_payment_method");
}
