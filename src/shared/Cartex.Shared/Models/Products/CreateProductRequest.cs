namespace Cartex.Shared.Models.Products;

public record CreateProductRequest(string Name, long? CategoryId, long UnitId, decimal MinStock, List<string>? Barcodes);
