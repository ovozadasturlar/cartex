namespace Cartex.Shared.Models.Sales;

public record CreateSaleItemRequest(long ProductId, decimal Quantity);

public record CreateSaleRequest(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemRequest> Items);
