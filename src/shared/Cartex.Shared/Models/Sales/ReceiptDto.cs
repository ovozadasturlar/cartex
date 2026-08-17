using System.Text.Json.Serialization;

namespace Cartex.Shared.Models.Sales;

public record ReceiptItemDto(string ProductName, decimal Quantity, string UnitName, decimal UnitPrice, decimal LineTotal, decimal DiscountAmount = 0)
{
    public bool HasDiscount => DiscountAmount > 0;
    public decimal NetTotal => LineTotal - DiscountAmount;
}

public record ReceiptPaymentDto(string Method, string Currency, decimal Amount, decimal Rate = 1m, decimal AmountBase = 0, bool IsForeign = false);

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
    List<ReceiptPaymentDto> Payments,
    long SaleId = 0,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? CustomerEmail = null,
    string? Language = null,
    string? BusinessPhone = null,
    string? BusinessTelegram = null,
    string? BusinessWebsite = null,
    string? LogoImageKey = null,
    decimal CreditAmount = 0,
    string? BaseCurrency = null,
    decimal PaidAdvance = 0,
    string? MonochromeLogoImageKey = null,
    long? CustomerId = null,
    string? Note = null,
    /// Savdo holati: bekor qilingan savdoni qayta tuzatib ham, qaytarib ham bo'lmaydi (QAYT-08).
    string Status = "Completed")
{
    public bool HasPayments => Payments.Count > 0;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// Chek chizilishidan oldin server logotipni saqlashdan yuklab shu yerga qo'yadi.
    /// Simdan hech qachon o'tmaydi — klientlar rasmni `LogoImageKey` orqali oladi.
    [JsonIgnore] public byte[]? LogoBytes { get; init; }
}
