using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Ordering;

public record CheckoutCartItemDto(long VariantId, decimal Quantity, decimal? UnitPrice = null)
{
    /// NARX-09: yakunlash ekranida ko'rsatilgan katalog narxi (faqat tekshirish uchun).
    public decimal? ExpectedUnitPrice { get; init; }
}

/// Everything past the tender is named, not positional: two adjacent optional decimals are one
/// slip away from paying out the rounding as a discount, and the compiler cannot see the swap.
public sealed record CheckoutCartRequest(decimal PaidCash, decimal PaidCard, decimal PaidBonus)
{
    public string? IdempotencyKey { get; init; }
    public List<CheckoutCartItemDto>? Items { get; init; }
    public List<SalePaymentRequest>? Payments { get; init; }
    public string? DebtCurrency { get; init; }
    public DateOnly? DebtDueDate { get; init; }
    public decimal? CreditAmount { get; init; }
    public bool? UseCustomerAdvance { get; init; }
    public long? CustomerId { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? Note { get; init; }
}
