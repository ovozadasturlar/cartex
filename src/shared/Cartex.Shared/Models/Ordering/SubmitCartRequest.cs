using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Ordering;

public record SubmitCartItemRequest(long VariantId, decimal Quantity, decimal? UnitPrice = null);

public record SubmitCartRequest(
    long WarehouseId,
    long? CustomerId,
    List<SubmitCartItemRequest> Items,
    string? IdempotencyKey = null,
    string? Note = null,
    string? Kind = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0,
    List<ParticipantSelectionRequest>? Participants = null,
    List<SalePaymentRequest>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal CreditAmount = 0,
    bool UseCustomerAdvance = true,
    decimal DiscountAmount = 0,
    decimal RoundingAmount = 0);
