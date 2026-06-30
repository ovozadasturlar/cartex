namespace Cartex.Shared.Models.Reports;

public record InventoryValuationGroupDto(string? Name, decimal Quantity, decimal Cost, decimal Retail);

public record InventoryValuationReportDto(decimal TotalCost, decimal TotalRetail, List<InventoryValuationGroupDto> ByWarehouse, List<InventoryValuationGroupDto> ByCategory);
