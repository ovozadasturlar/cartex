namespace Cartex.Shared.Models.Sales;

public record ReceiptItemDto(string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record ReceiptDto(
    string ReceiptToken,
    string BusinessName,
    string BranchName,
    string? BranchAddress,
    DateTime SaleDate,
    decimal TotalAmount,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    decimal DebtAmount,
    List<ReceiptItemDto> Items);
