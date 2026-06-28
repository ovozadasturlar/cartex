namespace Cartex.Shared.Models.Sales;

public record SaleDto(
    long Id,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    string Status,
    string ReceiptToken,
    string? CustomerName,
    string UserName);
