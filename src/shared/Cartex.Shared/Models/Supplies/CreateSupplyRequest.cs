namespace Cartex.Shared.Models.Supplies;

public record CreateSupplyItemRequest(long ProductId, decimal Quantity, decimal PurchasePrice, DateOnly? ExpiredAt);

public record CreateSupplyRequest(
    long SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemRequest> Items);
