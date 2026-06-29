namespace Cartex.Shared.Models.StockTransfers;

public record CreateStockTransferRequest(long FromWarehouseId, long ToWarehouseId, long VariantId, decimal Quantity);
