namespace Cartex.Shared.Models.Products;

public record ProductDto(long Id, string Name, string? CategoryName, string UnitName, decimal MinStock, List<string> Barcodes);
