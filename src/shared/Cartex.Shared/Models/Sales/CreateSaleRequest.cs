using Cartex.Shared.Models.Partners;

namespace Cartex.Shared.Models.Sales;

public record CreateSaleItemRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null, long? PrepackId = null);

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
