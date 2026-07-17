namespace Cartex.Shared.Models.Supplies;

public record SupplyItemDto(long VariantId, string ProductName, string UnitName, decimal Quantity, long? UnitId, decimal PackSize, decimal PurchasePrice, DateOnly? ExpiredAt)
{
    public decimal Total => Quantity * PurchasePrice;
}

public record SupplyDetailDto(
    long Id,
    DateOnly SupplyDate,
    long SupplierId,
    string SupplierName,
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
