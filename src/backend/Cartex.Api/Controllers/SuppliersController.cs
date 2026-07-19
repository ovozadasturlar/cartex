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
    public async Task<ActionResult<IReadOnlyCollection<SupplierDto>>> GetSuppliers([FromQuery] GetSuppliersQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Suppliers.View)]
    public async Task<ActionResult<SupplierTotalsDto>> GetTotals([FromQuery] GetSupplierTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:long}/ledger")]
    [HasPermission(AppPermissions.Suppliers.View)]
    public async Task<ActionResult<IReadOnlyCollection<SupplierLedgerEntryDto>>> GetLedger(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await sender.Send(new GetSupplierLedgerQuery(id, page, pageSize));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Suppliers.Manage)]
    public async Task<ActionResult<long>> CreateSupplier(CreateSupplierCommand command)
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

    [HttpGet("{id:long}/payments")]
    [HasPermission(AppPermissions.Suppliers.Manage)]
    public async Task<ActionResult<IReadOnlyCollection<SupplierPaymentDto>>> GetPayments(long id, [FromQuery] DateOnly date)
    {
        var result = await sender.Send(new GetSupplierPaymentsQuery(id, date));
        return Ok(result);
    }
}
