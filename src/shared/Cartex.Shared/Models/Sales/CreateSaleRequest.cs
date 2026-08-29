using Cartex.Shared.Models.Partners;

namespace Cartex.Shared.Models.Sales;

public record CreateSaleItemRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null, long? PrepackId = null)
{
    /// NARX-09: klient ekranda ko'rsatgan katalog narxi — savdo aynan shu narxda yakunlanadi.
    /// Server uni narx tarixidan tanib qabul qiladi (`NARX-10`), tanimasa savdo yaratilmaydi.
    public decimal? ExpectedUnitPrice { get; init; }
}

public record SalePaymentRequest(string Method, string Currency, decimal Amount);

public sealed record CreateSaleRequest(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    decimal PaidBonus,
    List<CreateSaleItemRequest> Items)
{
    public decimal DiscountAmount { get; init; }
    public List<SalePaymentRequest>? Payments { get; init; }
    public string? DebtCurrency { get; init; }
    public DateOnly? DebtDueDate { get; init; }
    public string? IdempotencyKey { get; init; }
    public bool ApplyAutoDiscount { get; init; } = true;
    public decimal CreditAmount { get; init; }
    public bool UseCustomerAdvance { get; init; } = true;
    public List<ParticipantSelectionRequest>? Participants { get; init; }
    public string? Note { get; init; }
}

/// NARX-09: savdo `price_changed` bilan rad etilganda qaytadigan tafsilot — qaysi mahsulot,
/// klient ko'rgan narx va serverdagi joriy narx.
public sealed record PriceChangeDto(long VariantId, string ProductName, decimal Expected, decimal Current);
