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
[Route("api/customer-refunds")]
[Authorize]
public sealed class CustomerRefundsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerRefundListDto>>> Get(
        [FromQuery] GetCustomerRefundsQuery query) => Ok(await sender.Send(query));

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<CustomerRefundDocumentDto>> GetById(long id) =>
        Ok(await sender.Send(new GetCustomerRefundByIdQuery(id)));

    [HttpPost]
    [HasPermission(AppPermissions.Customers.Refund)]
    public async Task<ActionResult<CustomerRefundCreatedDto>> Create(CreateCustomerRefundRequest request) =>
        Ok(await sender.Send(new CreateCustomerRefundCommand(
            request.CustomerId,
            request.BranchId,
            request.Tenders.Select(x => new CustomerRefundTenderInput(
                ParseMethod(x.Method), x.Currency, x.Amount)).ToList(),
            request.BusinessDate,
            request.Note,
            request.IdempotencyKey)));

    private static PaymentMethod ParseMethod(string value) =>
        Enum.TryParse<PaymentMethod>(value, true, out var method)
            ? method
            : throw new BusinessRuleException($"Qaytarish turi noto'g'ri: {value}", "invalid_payment_method");
}
