using Cartex.Application.Supplies.Commands;
using Cartex.Application.Supplies.Import;
using Cartex.Application.Supplies.Queries;
using Cartex.Application.Suppliers.Commands;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AttachSupplierPaymentsRequest = Cartex.Shared.Models.Supplies.AttachSupplierPaymentsRequest;
using Cartex.Shared.Models.Supplies;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiresFeature(FeatureCatalog.Supplies)]
public class SuppliesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Supplies.View)]
    public async Task<ActionResult<IReadOnlyCollection<SupplyDto>>> GetSupplies([FromQuery] GetSuppliesQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Supplies.View)]
    public async Task<ActionResult<SuppliesTotalsDto>> GetTotals([FromQuery] GetSuppliesTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [HasPermission(AppPermissions.Supplies.View)]
    public async Task<ActionResult<SupplyDetailDto>> GetSupply(long id)
    {
        var result = await sender.Send(new GetSupplyByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Supplies.Create)]
    public async Task<ActionResult<long>> CreateSupply(CreateSupplyCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Supplies.Edit)]
    public async Task<IActionResult> UpdateSupply(long id, UpdateSupplyCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPost("{id:long}/attach-payments")]
    [HasPermission(AppPermissions.Suppliers.Pay)]
    public async Task<IActionResult> AttachPayments(long id, AttachSupplierPaymentsRequest request)
    {
        await sender.Send(new AttachSupplierPaymentsCommand(id, request.TransactionIds));
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Supplies.Void)]
    public async Task<IActionResult> DeleteSupply(long id)
    {
        await sender.Send(new DeleteSupplyCommand(id));
        return NoContent();
    }

    [HttpGet("import/template")]
    [HasPermission(AppPermissions.Supplies.Import)]
    public async Task<IActionResult> ImportTemplate()
    {
        var content = await sender.Send(new GetSupplyImportTemplateQuery());
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cartex-kirim.xlsx");
    }

    [HttpPost("import/preview")]
    [HasPermission(AppPermissions.Supplies.Import)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<SupplyImportPreviewDto>> ImportPreview(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("Fayl tanlanmagan.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Faqat .xlsx fayl qabul qilinadi.");

        await using var content = file.OpenReadStream();
        var result = await sender.Send(new PreviewSupplyImportQuery(content));
        return Ok(result);
    }
}
