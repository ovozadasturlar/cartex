namespace Cartex.Shared.Models.Ordering;

public record CartLoadItemDto(long VariantId, string ProductName, string UnitName, decimal TotalQuantity);
