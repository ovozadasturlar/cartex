namespace Cartex.Shared.Models.Ordering;

public record CartItemDto(long VariantId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

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
    decimal PaidBonus = 0);

public record CartListDto(long Id, string AggregateCode, string Status, string? CustomerName, string WarehouseName, int ItemCount, DateTime CreatedAt, string? CreatedByName, string? Note, decimal EstimatedTotal);

public record UpdateCartStatusRequest(string Status);

public record UpdateCartItemsRequest(List<SubmitCartItemRequest> Items);
