using Cartex.Shared.Models.Products;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IProductsApi
{
    [Get("/api/products")]
    Task<List<ProductDto>> GetAllAsync([Query] long? categoryId = null, [Query] string? search = null);

    [Get("/api/products/by-barcode")]
    Task<ProductLookupDto> GetByBarcodeAsync([Query] string code, [Query] long warehouseId);

    [Post("/api/products")]
    Task<long> CreateAsync([Body] CreateProductRequest request);

    [Put("/api/products/{id}")]
    Task UpdateAsync(long id, [Body] UpdateProductRequest request);
}
