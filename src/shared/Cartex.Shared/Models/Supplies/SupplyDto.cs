namespace Cartex.Shared.Models.Supplies;

public record SupplyDto(long Id, DateOnly SupplyDate, decimal TotalAmount, string? SupplierName, string WarehouseName, string UserName);
