using Cartex.Application.Reports.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cartex.Shared.Models.Reports;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
[RequiresFeature(FeatureCatalog.Reports)]
public class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet("sales")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<SalesReportDto>> GetSalesReport([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] long? warehouseId, [FromQuery] int? tzOffsetMinutes = null)
    {
        var result = await sender.Send(new GetSalesReportQuery(from, to, warehouseId, tzOffsetMinutes));
        return Ok(result);
    }

    [HttpGet("cash-flow")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<IReadOnlyCollection<DailyCashFlowDto>>> GetCashFlow([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] int? tzOffsetMinutes = null)
    {
        var result = await sender.Send(new GetCashFlowQuery(from, to, tzOffsetMinutes));
        return Ok(result);
    }

    [HttpGet("debt-aging")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<DebtAgingReportDto>> GetDebtAgingReport()
    {
        var result = await sender.Send(new GetDebtAgingReportQuery());
        return Ok(result);
    }

    [HttpGet("inventory-valuation")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<InventoryValuationReportDto>> GetInventoryValuationReport([FromQuery] long? warehouseId)
    {
        var result = await sender.Send(new GetInventoryValuationReportQuery(warehouseId));
        return Ok(result);
    }

    [HttpGet("sales-breakdown")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<SalesBreakdownReportDto>> GetSalesBreakdownReport([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] long? warehouseId)
    {
        var result = await sender.Send(new GetSalesBreakdownReportQuery(from, to, warehouseId));
        return Ok(result);
    }

    [HttpGet("top-customers")]
    [HasPermission(AppPermissions.Reports.View)]
    public async Task<ActionResult<List<CustomerSalesDto>>> GetTopCustomersReport([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] long? warehouseId)
    {
        var result = await sender.Send(new GetTopCustomersReportQuery(from, to, warehouseId));
        return Ok(result);
    }
}
