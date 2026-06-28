namespace Cartex.Shared.Models.Products;

public record ProductTypeDto(long Id, string Name, bool TracksExpiry, string MeasureMode);

public record CreateProductTypeRequest(string Name, bool TracksExpiry, string MeasureMode);
