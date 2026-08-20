namespace Cartex.Shared.Models.Sales;

/// Ogohlantirish savdoni bekor qilmaydi — kassirga ko'rsatiladigan kod (masalan
/// `stock_negative_offline`, OFF-17).
public record CreateSaleResult(long SaleId, string ReceiptToken, IReadOnlyList<string>? Warnings = null);
