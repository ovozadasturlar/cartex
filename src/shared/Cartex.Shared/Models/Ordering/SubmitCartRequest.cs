namespace Cartex.Shared.Models.Ordering;

public record SubmitCartItemRequest(long VariantId, decimal Quantity);

public record SubmitCartRequest(
    long WarehouseId,
    long? CustomerId,
    List<SubmitCartItemRequest> Items,
    string? IdempotencyKey = null,
    string? Note = null,
    string? Kind = null,
    decimal PaidCash = 0,
    decimal PaidCard = 0,
    decimal PaidBonus = 0);
