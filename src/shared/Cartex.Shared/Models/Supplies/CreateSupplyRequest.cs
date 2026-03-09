namespace Cartex.Shared.Models.Supplies;

public record CreateSupplyItemRequest(long ProductId, decimal Quantity, decimal PurchasePrice);

public record CreateSupplyRequest(
    long SupplierId,
    long WarehouseId,
    long UserId,
    DateOnly SupplyDate,
    List<CreateSupplyItemRequest> Items);
