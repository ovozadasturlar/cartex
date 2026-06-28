namespace Cartex.Shared.Models.Ordering;

public record CartItemDto(long ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record CartDto(string AggregateCode, string Status, long WarehouseId, long? CustomerId, string? CustomerName, decimal Total, List<CartItemDto> Items);
