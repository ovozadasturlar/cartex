using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Notifications;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerDto>>> GetCustomers([FromQuery] GetCustomersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Customers.Create)]
    public async Task<ActionResult<long>> CreateCustomer(CreateCustomerCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Customers.Edit)]
    public async Task<IActionResult> UpdateCustomer(long id, UpdateCustomerCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Customers.Delete)]
    public async Task<IActionResult> DeleteCustomer(long id)
    {
        await sender.Send(new DeleteCustomerCommand(id));
        return NoContent();
    }

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<CustomerDto>> GetById(long id)
    {
        var customer = await sender.Send(new GetCustomerByIdQuery(id));
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("{id:long}/messages")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<IReadOnlyCollection<NotificationDeliveryDto>>> GetMessages(long id)
    {
        var result = await sender.Send(new GetCustomerMessagesQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id}/ledger")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<IReadOnlyCollection<CustomerLedgerEntryDto>>> GetLedger(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await sender.Send(new GetCustomerLedgerQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:long}/statement")]
    [HasPermission(AppPermissions.Statements.View)]
    public async Task<ActionResult<CustomerStatementDto>> GetStatement(
        long id,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? documentTypes = null) =>
        Ok(await sender.Send(new GetCustomerStatementQuery(id, from, to, branchId, documentTypes)));

    [HttpGet("{id:long}/statement/export")]
    [HasPermission(AppPermissions.Statements.Export)]
    public async Task<IActionResult> ExportStatement(
        long id,
        [FromQuery] string format = "pdf",
        [FromQuery] string mode = "both",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] long? branchId = null,
        [FromQuery] string? documentTypes = null)
    {
        var document = await sender.Send(new ExportCustomerStatementCommand(
            id, format, mode, from, to, branchId, documentTypes));
        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpGet("by-card/{code}")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<CustomerDto>> GetByCard(string code)
    {
        var result = await sender.Send(new GetCustomerByCardQuery(code));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<ActionResult<CustomerTotalsDto>> GetTotals([FromQuery] GetCustomerTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost("{id}/message")]
    [HasPermission(AppPermissions.Customers.Message)]
    public async Task<IActionResult> SendMessage(long id, [FromBody] SendCustomerMessageRequest request)
    {
        await sender.Send(new SendCustomerMessageCommand(id, request.Channel, request.Text));
        return NoContent();
    }

    [HttpPost("{id}/repay-debt")]
    [HasPermission(AppPermissions.Customers.ReceivePayment)]
    public async Task<IActionResult> RepayDebt(long id, [FromBody] RepayDebtRequest request)
    {
        await sender.Send(new RepayCustomerDebtCommand(id, request.Amount, request.ViaCard, request.DebtCurrency, request.PayCurrency, request.IdempotencyKey, request.WriteOff, request.WriteOffReason));
        return Ok();
    }

    // A read that takes a body: the selection can be dozens of ids, which do not belong in a URL.
    [HttpPost("{id}/consolidated-act")]
    [HasPermission(AppPermissions.Customers.Act)]
    public async Task<ActionResult<ConsolidatedActDto>> ConsolidatedAct(long id, [FromBody] ConsolidatedActRequest request)
    {
        var result = await sender.Send(new GetConsolidatedActQuery(id, request.Documents));
        return Ok(result);
    }

    [HttpPost("{id}/bonus")]
    [HasPermission(AppPermissions.Loyalty.GrantBonus)]
    public async Task<IActionResult> GiveBonus(long id, [FromBody] GiveCustomerBonusRequest request)
    {
        await sender.Send(new GiveCustomerBonusCommand(id, request.Amount, request.Note));
        return Ok();
    }
}
