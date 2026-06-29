namespace Cartex.Application.Common.Interfaces;

public record ProductCatalogInfo(string? Name, string? Brand, string? ImageUrl);

public interface IProductCatalogProvider
{
    Task<ProductCatalogInfo?> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
}
