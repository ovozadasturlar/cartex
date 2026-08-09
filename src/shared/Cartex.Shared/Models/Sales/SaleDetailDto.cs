namespace Cartex.Shared.Models.Sales;

public sealed record SaleDetailItemDto(
    long SaleItemId,
    long VariantId,
    string ProductName,
    string? VariantName,
    string UnitName,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal ReturnableQuantity,
    decimal UnitPrice,
    string PriceCurrency,
    decimal PriceRate,
    decimal LineTotal,
    decimal CashbackEarned,
    decimal ReturnedCashback,
    decimal QuantityStep = 1,
    bool AllowsFractional = false);

public sealed record SaleDetailPaymentDto(
    string Method,
    string Currency,
    decimal Amount,
    decimal Rate,
    decimal AmountBase);

public sealed record SaleDetailParticipantDto(
    long RoleDefinitionId,
    long PartyId,
    string RoleLabel,
    string PartyName,
    string? PartyPhone,
    string Source);

public sealed record SaleReturnSummaryDto(
    long Id,
    string DocumentNumber,
    DateOnly BusinessDate,
    decimal RefundAmount,
    bool IsFullReturn,
    string Status);

public sealed record SaleDetailDto(
    long Id,
    DateTime SaleDate,
    string Status,
    string ReceiptToken,
    long BranchId,
    string BranchName,
    long WarehouseId,
    string WarehouseName,
    long UserId,
    string UserName,
    long? CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    long? TradeCaseId,
    string? TradeCaseNumber,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal PaidAdvance,
    decimal DebtAmount,
    string DebtCurrency,
    decimal ChangeAmount,
    decimal CreditAmount,
    decimal CashbackEarned,
    IReadOnlyList<SaleDetailItemDto> Items,
    IReadOnlyList<SaleDetailPaymentDto> Payments,
    IReadOnlyList<SaleDetailParticipantDto> Participants,
    IReadOnlyList<SaleReturnSummaryDto> Returns,
    IReadOnlyList<string> AllowedActions);
