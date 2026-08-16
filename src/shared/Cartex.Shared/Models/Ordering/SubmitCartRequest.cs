using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Ordering;

public record SubmitCartItemRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public sealed record SubmitCartRequest(long WarehouseId, long? CustomerId, List<SubmitCartItemRequest> Items)
{
    public string? IdempotencyKey { get; init; }
    public string? Note { get; init; }
    public string? Kind { get; init; }
    public decimal PaidCash { get; init; }
    public decimal PaidCard { get; init; }
    public decimal PaidBonus { get; init; }
    public List<ParticipantSelectionRequest>? Participants { get; init; }
    public List<SalePaymentRequest>? Payments { get; init; }
    public string? DebtCurrency { get; init; }
    public DateOnly? DebtDueDate { get; init; }
    public decimal CreditAmount { get; init; }
    public bool UseCustomerAdvance { get; init; } = true;
    public decimal DiscountAmount { get; init; }
    public decimal RoundingAmount { get; init; }
}
