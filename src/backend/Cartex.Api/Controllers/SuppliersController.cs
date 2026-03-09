using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Suppliers.Queries;
using Cartex.Auth.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission("suppliers.view")]
    public async Task<IActionResult> GetSuppliers()
    {
        var result = await sender.Send(new GetSuppliersQuery());
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("suppliers.manage")]
    public async Task<IActionResult> CreateSupplier(CreateSupplierCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }
}
