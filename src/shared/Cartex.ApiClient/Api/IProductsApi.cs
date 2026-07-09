using Cartex.Shared.Models.Products;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IProductsApi
{
    [Get("/api/products")]
    Task<List<ProductDto>> GetAllAsync([Query] long? categoryId = null, [Query] string? search = null);

    [Get("/api/products")]
    Task<IApiResponse<List<ProductDto>>> GetPagedAsync([Query] int page, [Query] int pageSize,
        [Query] string? sortBy = null, [Query] bool descending = false, [Query] string? search = null,
        [Query] long? categoryId = null);

    [Get("/api/products/totals")]
    Task<ProductsTotalsDto> GetTotalsAsync([Query] string? search = null, [Query] long? categoryId = null);

    [Get("/api/products/category-counts")]
    Task<List<CategoryCountDto>> GetCategoryCountsAsync();

    [Get("/api/products/lookup")]
    Task<List<ProductOptionDto>> GetLookupAsync();

    [Get("/api/products/by-barcode")]
    Task<ProductLookupDto> GetByBarcodeAsync([Query] string code, [Query] long warehouseId);

    [Get("/api/products/variants/{id}/price-info")]
    Task<VariantPriceInfoDto> GetVariantPriceInfoAsync(long id, [Query] long warehouseId);

    [Post("/api/products")]
    Task<long> CreateAsync([Body] CreateProductRequest request);

    [Put("/api/products/{id}")]
    Task UpdateAsync(long id, [Body] UpdateProductRequest request);

    [Post("/api/products/price")]
    Task SetPriceAsync([Body] SetProductPriceRequest request);

    [Get("/api/products/{productId}/variants")]
    Task<List<VariantDto>> GetVariantsAsync(long productId);

    [Post("/api/products/{productId}/variants")]
    Task<long> CreateVariantAsync(long productId, [Body] CreateVariantRequest request);

    [Put("/api/products/variants/{id}")]
    Task UpdateVariantAsync(long id, [Body] UpdateVariantRequest request);

    [Delete("/api/products/variants/{id}")]
    Task DeleteVariantAsync(long id);
}
