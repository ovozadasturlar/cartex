namespace Cartex.Shared.Models.Sales;

public record CreateSaleItemRequest(long ProductId, long StockId, decimal Quantity, decimal UnitPrice);

public record CreateSaleRequest(
    long WarehouseId,
    long UserId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemRequest> Items);
