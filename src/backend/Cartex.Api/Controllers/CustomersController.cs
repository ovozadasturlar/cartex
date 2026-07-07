using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Shared.Models.Customers;
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
    public async Task<IActionResult> GetCustomers([FromQuery] GetCustomersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Customers.Manage)]
    public async Task<IActionResult> CreateCustomer(CreateCustomerCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Customers.Manage)]
    public async Task<IActionResult> UpdateCustomer(long id, UpdateCustomerCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<IActionResult> GetById(long id)
    {
        var customer = await sender.Send(new GetCustomerByIdQuery(id));
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet("{id}/ledger")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<IActionResult> GetLedger(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await sender.Send(new GetCustomerLedgerQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpGet("by-card/{code}")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<IActionResult> GetByCard(string code)
    {
        var result = await sender.Send(new GetCustomerByCardQuery(code));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Customers.View)]
    public async Task<IActionResult> GetTotals([FromQuery] GetCustomerTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost("{id}/repay-debt")]
    [HasPermission(AppPermissions.Customers.Manage)]
    public async Task<IActionResult> RepayDebt(long id, [FromBody] RepayDebtRequest request)
    {
        await sender.Send(new RepayCustomerDebtCommand(id, request.Amount, request.ViaCard, request.DebtCurrency, request.PayCurrency));
        return Ok();
    }

    [HttpPost("{id}/bonus")]
    [HasPermission(AppPermissions.Loyalty.Manage)]
    public async Task<IActionResult> GiveBonus(long id, [FromBody] GiveCustomerBonusRequest request)
    {
        await sender.Send(new GiveCustomerBonusCommand(id, request.Amount, request.Note));
        return Ok();
    }
}
