namespace Cartex.Shared.Models.Sales;

/// <summary>
/// Lightweight projection for high-volume sale lists. Full lines and payments are
/// intentionally loaded from the sale detail endpoint only after navigation.
/// </summary>
public sealed record SaleListDto(
    long Id,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal PaidAdvance,
    decimal DebtAmount,
    decimal CreditAmount,
    string Status,
    string ReceiptToken,
    long? CustomerId,
    string? CustomerName,
    string UserName,
    int ItemCount,
    string? FirstItemName,
    string? SecondItemName,
    bool CanResendReceipt);
