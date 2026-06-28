using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Auth.Authorization;
using Cartex.Shared.Models.Customers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("customers.view")]
    public async Task<IActionResult> GetCustomers([FromQuery] GetCustomersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("customers.manage")]
    public async Task<IActionResult> CreateCustomer(CreateCustomerCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpGet("{id}/ledger")]
    [HasPermission("customers.view")]
    public async Task<IActionResult> GetLedger(long id)
    {
        var result = await sender.Send(new GetCustomerLedgerQuery(id));
        return Ok(result);
    }

    [HttpPost("{id}/repay-debt")]
    [HasPermission("customers.manage")]
    public async Task<IActionResult> RepayDebt(long id, [FromBody] RepayDebtRequest request)
    {
        await sender.Send(new RepayCustomerDebtCommand(id, request.Amount, request.ViaCard));
        return Ok();
    }
}
