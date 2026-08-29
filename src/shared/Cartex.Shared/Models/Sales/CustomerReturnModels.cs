namespace Cartex.Shared.Models.Sales;

public record CustomerReturnLineRequest(
    long VariantId,
    decimal Quantity,
    long? SaleItemId = null,
    decimal? UnitPrice = null,
    string? Reason = null,
    string Condition = "Sellable",
    string Disposition = "SellableRestock");

public record CustomerReturnSettlementRequest(string Method, string Currency, decimal Amount);

public record CreateCustomerReturnRequest(
    long WarehouseId,
    List<CustomerReturnLineRequest> Lines,
    long? CustomerId = null,
    List<CustomerReturnSettlementRequest>? Settlements = null,
    bool AutoSettle = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public record CustomerReturnCreatedDto(
    long Id,
    string DocumentNumber,
    decimal RefundAmount);

public record CustomerReturnLineDto(
    long Id,
    long? SaleId,
    long? SaleItemId,
    long VariantId,
    string ProductName,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineAmount,
    decimal CashbackReversed,
    string? Reason,
    string Condition,
    string Disposition);

public record CustomerReturnSettlementDto(
    string Method,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase);

public record CustomerReturnDocumentDto(
    long Id,
    string DocumentNumber,
    long BranchId,
    long WarehouseId,
    string WarehouseName,
    long? CustomerId,
    string? CustomerName,
    long UserId,
    string UserName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal GrossAmount,
    decimal RefundAmount,
    decimal CashbackReversed,
    string? Note,
    IReadOnlyList<CustomerReturnLineDto> Lines,
    IReadOnlyList<CustomerReturnSettlementDto> Settlements);

public record CustomerReturnListDto(
    long Id,
    string DocumentNumber,
    long? CustomerId,
    string? CustomerName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    int LineCount,
    decimal RefundAmount,
    string? Note);

public record VariantSalePriceDto(decimal UnitPrice, DateTime LastSoldAt);
