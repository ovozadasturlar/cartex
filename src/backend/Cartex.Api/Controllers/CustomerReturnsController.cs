using Cartex.Application.Common.Messaging;
using Cartex.Application.CustomerReturns.Commands;
using Cartex.Application.CustomerReturns.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Shared.Models.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/customer-returns")]
[Authorize]
public class CustomerReturnsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Returns.View)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerReturnListDto>>> Get(
        [FromQuery] GetCustomerReturnsQuery query) => Ok(await sender.Send(query));

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Returns.View)]
    public async Task<ActionResult<CustomerReturnDocumentDto>> GetById(long id) =>
        Ok(await sender.Send(new GetCustomerReturnByIdQuery(id)));

    [HttpPost]
    [HasPermission(AppPermissions.Returns.Create)]
    public async Task<ActionResult<CustomerReturnCreatedDto>> Create(CreateCustomerReturnRequest request)
    {
        var lines = request.Lines.Select(x => new CustomerReturnLineInput(
            x.SaleItemId,
            x.Quantity,
            x.Reason,
            Parse<ReturnItemCondition>(x.Condition, "return_condition"),
            Parse<InventoryDisposition>(x.Disposition, "inventory_disposition"))).ToList();
        var settlements = request.Settlements?.Select(x => new CustomerReturnSettlementInput(
            Parse<ReturnSettlementMethod>(x.Method, "return_settlement_method"),
            x.Currency,
            x.Amount)).ToList();

        return Ok(await sender.Send(new CreateCustomerReturnCommand(
            request.SaleId,
            lines,
            settlements,
            request.AutoSettle,
            request.BusinessDate,
            request.Note,
            request.IdempotencyKey)));
    }

    private static T Parse<T>(string value, string code) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var result)
            ? result
            : throw new BusinessRuleException($"Qiymat noto'g'ri: {value}", code);
}
