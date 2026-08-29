using Cartex.Application.Common.Messaging;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.CustomerPayments.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/customer-payments")]
[Authorize]
public class CustomerPaymentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.CustomerPayments.View)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerPaymentListDto>>> Get(
        [FromQuery] GetCustomerPaymentsQuery query)
    {
        return Ok(await sender.Send(query));
    }

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.CustomerPayments.View)]
    public async Task<ActionResult<CustomerPaymentDocumentDto>> GetById(long id)
    {
        return Ok(await sender.Send(new GetCustomerPaymentByIdQuery(id)));
    }

    [HttpPost("{id:long}/void")]
    [HasPermission(AppPermissions.CustomerPayments.Void)]
    public async Task<IActionResult> Void(long id, VoidCustomerPaymentRequest request)
    {
        await sender.Send(new VoidCustomerPaymentCommand(id, request.Reason));
        return NoContent();
    }

    [HttpPost]
    [HasPermission(AppPermissions.CustomerPayments.Create)]
    public async Task<ActionResult<CustomerPaymentCreatedDto>> Create(CreateCustomerPaymentRequest request)
    {
        var tenders = request.Tenders.Select(x => new CustomerPaymentTenderInput(
            ParseMethod(x.Method), x.Currency, x.Amount)).ToList();
        var allocations = request.Allocations?.Select(x => new CustomerPaymentAllocationInput(
            x.Currency, x.Amount, x.SaleId)).ToList();
        var result = await sender.Send(new CreateCustomerPaymentCommand(
            request.CustomerId,
            request.BranchId,
            tenders,
            allocations,
            request.AutoAllocateDebt,
            request.BusinessDate,
            request.Note,
            request.IdempotencyKey));
        return Ok(result);
    }

    private static PaymentMethod ParseMethod(string value) =>
        Enum.TryParse<PaymentMethod>(value, true, out var method)
            ? method
            : throw new BusinessRuleException($"To'lov turi noto'g'ri: {value}", "invalid_payment_method");
}
