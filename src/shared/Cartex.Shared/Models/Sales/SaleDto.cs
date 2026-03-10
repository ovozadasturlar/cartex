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
    string? CustomerName,
    string UserName)
{
    public string DisplayDate => SaleDate.ToString("dd.MM.yyyy HH:mm");
    public string DisplayTotal => TotalAmount.ToString("N0");
    public string DisplayCash => PaidCash.ToString("N0");
    public string DisplayCard => PaidCard.ToString("N0");
    public string DisplayDebt => DebtAmount.ToString("N0");
}
