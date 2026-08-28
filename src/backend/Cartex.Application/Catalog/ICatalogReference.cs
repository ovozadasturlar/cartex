using Cartex.Shared.Models.Catalog;

namespace Cartex.Application.Catalog;

public interface ICatalogReference
{
    Task<CatalogProductDto?> ByBarcodeAsync(string? barcode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogProductDto>> SearchAsync(string? query, int limit, CancellationToken cancellationToken = default);
}

public interface ICatalogSource
{
    CatalogSourceMode Mode { get; }

    Task<CatalogProductDto?> ByBarcodeAsync(string barcode, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogProductDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
