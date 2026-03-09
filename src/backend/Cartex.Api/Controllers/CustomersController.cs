using Cartex.Application.Customers.Commands;
using Cartex.Application.Customers.Queries;
using Cartex.Auth.Authorization;
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
    public async Task<IActionResult> GetCustomers([FromQuery] string? search)
    {
        var result = await sender.Send(new GetCustomersQuery(search));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("customers.manage")]
    public async Task<IActionResult> CreateCustomer(CreateCustomerCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
