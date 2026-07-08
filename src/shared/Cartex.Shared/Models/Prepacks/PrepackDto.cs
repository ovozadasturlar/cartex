namespace Cartex.Shared.Models.Prepacks;

public record PrepackDto(long Id, string LabelCode, string ProductName, string UnitName, decimal Quantity, decimal Price, string Status, DateTime? ExpiresAt, DateTime CreatedAt);

public record PrepackLabelDto(long Id, string LabelCode, string ProductName, string UnitName, decimal Quantity, decimal Price);

public record PrepackLookupDto(long PrepackId, long VariantId, string ProductName, string UnitName, string Dimension, decimal Quantity, decimal UnitPrice, decimal Price);

public record CreatePrepacksRequest(long WarehouseId, long VariantId, decimal Quantity, int Count = 1, int? ExpiresHours = null);
