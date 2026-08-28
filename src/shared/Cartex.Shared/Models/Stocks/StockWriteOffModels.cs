namespace Cartex.Shared.Models.Stocks;

public record StockWriteOffCreatedDto(long Id, string DocumentNumber, decimal TotalCost, decimal SupplierClaimAmount);

public record StockWriteOffLineDto(
    long Id,
    long VariantId,
    string ProductName,
    long StockId,
    decimal Quantity,
    decimal UnitCost,
    decimal LineCost,
    string Reason,
    string Disposition,
    long? SupplierId,
    string? SupplierName,
    string? Note);

public record StockWriteOffDto(
    long Id,
    string DocumentNumber,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    long WarehouseId,
    string WarehouseName,
    string? UserName,
    decimal TotalCost,
    decimal SupplierClaimAmount,
    long? ReversesDocumentId,
    string? Note,
    IReadOnlyList<StockWriteOffLineDto> Lines);

public record WriteOffBatchDto(
    long StockId,
    long VariantId,
    decimal Quantity,
    decimal PurchasePrice,
    DateOnly? ExpiredAt,
    long? SupplierId,
    string? SupplierName,
    bool SupplierAcceptsReturns);

public record WriteOffBalanceDto(
    long WarehouseId,
    string WarehouseName,
    string Location,
    long VariantId,
    string ProductName,
    decimal Quantity);

public record StockWriteOffLineRequest(
    long VariantId,
    decimal Quantity,
    string Reason,
    string Disposition,
    long? StockId = null,
    string? Note = null);

public record CreateStockWriteOffRequest(
    long WarehouseId,
    IReadOnlyList<StockWriteOffLineRequest> Lines,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public record ReverseStockWriteOffRequest(string? Note = null, string? IdempotencyKey = null);
