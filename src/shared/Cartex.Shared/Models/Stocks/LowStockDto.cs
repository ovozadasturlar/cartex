namespace Cartex.Shared.Models.Stocks;

public record LowStockDto(long VariantId, string ProductName, string UnitName, string WarehouseName, decimal OnHand, decimal MinStock);
