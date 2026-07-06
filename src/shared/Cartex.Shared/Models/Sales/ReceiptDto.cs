namespace Cartex.Shared.Models.Sales;

public record ReceiptItemDto(string ProductName, decimal Quantity, string UnitName, decimal UnitPrice, decimal LineTotal);

public record ReceiptPaymentDto(string Method, string Currency, decimal Amount);

public record ReceiptDto(
    string ReceiptToken,
    string BusinessName,
    string BranchName,
    string? BranchAddress,
    string? BranchPhone,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    decimal ChangeAmount,
    decimal CashbackEarned,
    string UserName,
    List<ReceiptItemDto> Items,
    List<ReceiptPaymentDto> Payments);
