namespace Cartex.Shared.Models.Stocks;

public record InventoryMovementDto(
    long Id,
    DateTime OccurredAt,
    string Kind,
    string FromLocationKind,
    long FromLocationId,
    string ToLocationKind,
    long ToLocationId,
    long WarehouseId,
    string WarehouseName,
    decimal Quantity,
    string? UserName,
    string SourceType,
    long? SourceId);
