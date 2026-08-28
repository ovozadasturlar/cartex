using Cartex.Shared.Models.Catalog;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICatalogApi
{
    [Get("/api/catalog/by-barcode")]
    Task<CatalogProductDto?> GetByBarcodeAsync([Query] string code);

    [Get("/api/catalog/search")]
    Task<List<CatalogProductDto>> SearchAsync([Query] string? q, [Query] int limit = 10);
}
