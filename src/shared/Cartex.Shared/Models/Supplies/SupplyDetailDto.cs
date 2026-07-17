namespace Cartex.Shared.Models.Supplies;

public record SupplyItemDto(
    long VariantId,
    string ProductName,
    string UnitName,
    decimal Quantity,
    long? UnitId,
    decimal PackSize,
    decimal PurchasePrice,
    DateOnly? ExpiredAt,
    long? PackId = null,
    decimal EntryQuantity = 0,
    decimal EntryPrice = 0,
    string PriceBasis = "PerEntry")
{
    public decimal Total => Quantity * PurchasePrice;
}

public record SupplyDetailDto(
    long Id,
    DateOnly SupplyDate,
    long SupplierId,
    string SupplierName,
    long WarehouseId,
    string WarehouseName,
    string UserName,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidTransfer,
    decimal PaidBank,
    string Currency,
    decimal Rate,
    List<SupplyItemDto> Items);
