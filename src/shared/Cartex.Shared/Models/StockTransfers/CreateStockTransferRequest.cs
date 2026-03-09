namespace Cartex.Shared.Models.StockTransfers;

public record CreateStockTransferRequest(long FromWarehouseId, long ToWarehouseId, long ProductId, decimal Quantity, long UserId);
