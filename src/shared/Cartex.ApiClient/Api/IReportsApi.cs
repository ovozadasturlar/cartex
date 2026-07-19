using Cartex.Shared.Models.Reports;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IReportsApi
{
    [Get("/api/reports/sales")]
    Task<SalesReportDto> GetSalesReportAsync([Query] DateTime from, [Query] DateTime to, [Query] long? warehouseId = null, [Query] int? tzOffsetMinutes = null);

    [Get("/api/reports/cash-flow")]
    Task<List<DailyCashFlowDto>> GetCashFlowAsync([Query] DateTime from, [Query] DateTime to, [Query] int? tzOffsetMinutes = null);

    [Get("/api/reports/debt-aging")]
    Task<DebtAgingReportDto> GetDebtAgingReportAsync();

    [Get("/api/reports/inventory-valuation")]
    Task<InventoryValuationReportDto> GetInventoryValuationReportAsync([Query] long? warehouseId = null);

    [Get("/api/reports/sales-breakdown")]
    Task<SalesBreakdownReportDto> GetSalesBreakdownReportAsync([Query] DateTime from, [Query] DateTime to, [Query] long? warehouseId = null);

    [Get("/api/reports/top-customers")]
    Task<List<CustomerSalesDto>> GetTopCustomersAsync([Query] DateTime from, [Query] DateTime to, [Query] long? warehouseId = null);
}
