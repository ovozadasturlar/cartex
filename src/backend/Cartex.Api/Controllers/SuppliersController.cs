using Cartex.Application.Suppliers.Commands;
using Cartex.Application.Suppliers.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Suppliers)]
public class SuppliersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Suppliers.View)]
    public async Task<IActionResult> GetSuppliers([FromQuery] GetSuppliersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Suppliers.Manage)]
    public async Task<IActionResult> CreateSupplier(CreateSupplierCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Suppliers.Manage)]
    public async Task<IActionResult> UpdateSupplier(long id, UpdateSupplierCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPost("{id:long}/pay-debt")]
    [HasPermission(AppPermissions.Suppliers.Manage)]
    public async Task<IActionResult> PayDebt(long id, PaySupplierDebtCommand command)
    {
        await sender.Send(command with { SupplierId = id });
        return Ok();
    }
}
