namespace Cartex.Shared.Models.Supplies;

public record CreateSupplyItemRequest(long VariantId, decimal Quantity, decimal PurchasePrice, DateOnly? ExpiredAt, long? UnitId = null, decimal? SellingPrice = null);

public record CreateSupplyRequest(
    long SupplierId,
    long WarehouseId,
    DateOnly SupplyDate,
    List<CreateSupplyItemRequest> Items,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    string? Currency = null);
