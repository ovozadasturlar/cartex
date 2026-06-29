namespace Cartex.Shared.Models.Products;

public record ProductTypeDto(long Id, string Name, bool TracksExpiry, string MeasureMode, string? AttributeSchema = null);

public record CreateProductTypeRequest(string Name, bool TracksExpiry, string MeasureMode, string? AttributeSchema = null);

public record UpdateProductTypeRequest(string Name, bool TracksExpiry, string MeasureMode, string? AttributeSchema = null);
