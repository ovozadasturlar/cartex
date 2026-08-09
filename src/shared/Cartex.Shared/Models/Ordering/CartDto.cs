using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Sales;

namespace Cartex.Shared.Models.Ordering;

public record CartItemDto(
    long VariantId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string UnitName = "",
    decimal QuantityStep = 1,
    bool AllowsFractional = false,
    string? ImageKey = null);

public sealed record CartParticipantDto(
    long RoleDefinitionId,
    long PartyId,
    string PartyName,
    string? PartyPhone,
    string RoleLabel);

public sealed record CartPaymentDto(string Method, string Currency, decimal Amount);

public record CartDto(
    string AggregateCode,
    string Status,
    long WarehouseId,
    long? CustomerId,
    string? CustomerName,
    decimal Total,
    List<CartItemDto> Items,
    string? Note,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0,
    List<CartParticipantDto>? Participants = null,
    List<CartPaymentDto>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal CreditAmount = 0,
    bool UseCustomerAdvance = true,
    long Id = 0,
    string Kind = "Queue",
    int Version = 1,
    long? CreatedByUserId = null,
    string? CreatedByName = null,
    long? ClaimedByUserId = null,
    string? ClaimedByName = null,
    DateTime? ClaimedAt = null,
    long? SaleId = null,
    DateTime? CancelledAt = null,
    string? CancellationReason = null,
    long? RequeuedFromCartId = null,
    List<string>? AllowedActions = null);

public record CartListDto(long Id, string AggregateCode, string Status, string? CustomerName,
    string WarehouseName, int ItemCount, DateTime CreatedAt, string? CreatedByName,
    string? Note, decimal EstimatedTotal, string Kind = "Queue", int Version = 1,
    long? ClaimedByUserId = null, string? ClaimedByName = null, DateTime? ClaimedAt = null,
    long? SaleId = null, string? CancellationReason = null);

public record UpdateCartStatusRequest(string Status, string? Reason = null);

public record UpdateCartItemsRequest(List<SubmitCartItemRequest> Items, int? ExpectedVersion = null);

public sealed record UpdateCartRequest(
    long? CustomerId,
    List<SubmitCartItemRequest> Items,
    string? Note = null,
    List<ParticipantSelectionRequest>? Participants = null,
    List<SalePaymentRequest>? Payments = null,
    string? DebtCurrency = null,
    DateOnly? DebtDueDate = null,
    decimal CreditAmount = 0,
    bool UseCustomerAdvance = true,
    int? ExpectedVersion = null);

public sealed record RequeueCartRequest(string? Note = null, string? IdempotencyKey = null);
public sealed record RequeueCartResult(string AggregateCode, int Version);
