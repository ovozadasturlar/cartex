namespace Cartex.Shared.Models.Sales;

public record SaleLineDto(
    long SaleItemId,
    string ProductName,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal UnitPrice);

public record SaleDto(
    long Id,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    decimal CreditAmount,
    string Status,
    string ReceiptToken,
    string? CustomerName,
    string UserName,
    List<SaleLineDto> Items,
    bool CanResendReceipt = false);
