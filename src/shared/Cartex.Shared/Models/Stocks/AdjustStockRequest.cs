namespace Cartex.Shared.Models.Stocks;

public record AdjustStockRequest(long WarehouseId, long VariantId, decimal CountedQuantity, string? Reason);
