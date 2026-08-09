namespace Cartex.Shared.Models.Sales;

public record CustomerReturnLineRequest(
    long SaleItemId,
    decimal Quantity,
    string? Reason = null,
    string Condition = "Sellable",
    string Disposition = "SellableRestock");

public record CustomerReturnSettlementRequest(string Method, string Currency, decimal Amount);

public record CreateCustomerReturnRequest(
    long SaleId,
    List<CustomerReturnLineRequest> Lines,
    List<CustomerReturnSettlementRequest>? Settlements = null,
    bool AutoSettle = true,
    DateOnly? BusinessDate = null,
    string? Note = null,
    string? IdempotencyKey = null);

public record CustomerReturnCreatedDto(
    long Id,
    string DocumentNumber,
    decimal RefundAmount,
    bool IsFullReturn);

public record CustomerReturnLineDto(
    long Id,
    long SaleItemId,
    long VariantId,
    string ProductName,
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
    long? CustomerId,
    string? CustomerName,
    long SaleId,
    long UserId,
    string UserName,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal GrossAmount,
    decimal RefundAmount,
    decimal CashbackReversed,
    bool IsFullReturn,
    string? Note,
    IReadOnlyList<CustomerReturnLineDto> Lines,
    IReadOnlyList<CustomerReturnSettlementDto> Settlements);

public record CustomerReturnListDto(
    long Id,
    string DocumentNumber,
    long? CustomerId,
    string? CustomerName,
    long SaleId,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    string Status,
    decimal RefundAmount,
    bool IsFullReturn);
