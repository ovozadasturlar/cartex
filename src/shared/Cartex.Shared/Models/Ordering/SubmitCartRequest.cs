namespace Cartex.Shared.Models.Ordering;

public record SubmitCartItemRequest(long ProductId, decimal Quantity);

public record SubmitCartRequest(long WarehouseId, long? CustomerId, List<SubmitCartItemRequest> Items);
