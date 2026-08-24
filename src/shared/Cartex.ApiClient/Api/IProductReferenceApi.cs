using Cartex.Shared.Models.Products;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IProductReferenceApi
{
    [Get("/api/product-reference/by-barcode/{code}")]
    Task<ProductReferenceDto> GetByBarcodeAsync(string code);

    [Get("/api/product-reference")]
    Task<List<ProductReferenceDto>> SearchAsync([Query] string? search, [Query] int take = 20);
}
