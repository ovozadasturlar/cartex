namespace Cartex.Shared.Models.Ordering;

public record CartItemDto(long VariantId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record CartDto(string AggregateCode, string Status, long WarehouseId, long? CustomerId, string? CustomerName, decimal Total, List<CartItemDto> Items);

public record CartListDto(long Id, string AggregateCode, string Status, string? CustomerName, string WarehouseName, int ItemCount, DateTime CreatedAt);

public record UpdateCartStatusRequest(string Status);
