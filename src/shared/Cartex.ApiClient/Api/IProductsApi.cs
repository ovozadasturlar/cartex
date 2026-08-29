using Cartex.Shared.Models.Products;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IProductsApi
{
    [Get("/api/products")]
    Task<List<ProductDto>> GetAllAsync([Query] long? categoryId = null, [Query] string? search = null, [Query] long? variantId = null, [Query] decimal? minPrice = null, [Query] decimal? maxPrice = null);

    [Get("/api/products")]
    Task<IApiResponse<List<ProductDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/products/totals")]
    Task<ProductsTotalsDto> GetTotalsAsync([Query] string? search = null, [Query] long? categoryId = null, [Query] decimal? minPrice = null, [Query] decimal? maxPrice = null);

    [Get("/api/products/category-counts")]
    Task<List<CategoryCountDto>> GetCategoryCountsAsync();

    [Get("/api/products/lookup")]
    Task<List<ProductOptionDto>> GetLookupAsync();

    [Get("/api/products/by-barcode")]
    Task<ProductLookupDto> GetByBarcodeAsync([Query] string code, [Query] long warehouseId, [Query] bool forSale = false);

    [Get("/api/products/variants/{id}/price-info")]
    Task<VariantPriceInfoDto> GetVariantPriceInfoAsync(long id, [Query] long warehouseId);

    [Post("/api/products")]
    Task<long> CreateAsync([Body] CreateProductRequest request);

    [Put("/api/products/{id}")]
    Task UpdateAsync(long id, [Body] UpdateProductRequest request);

    [Put("/api/products/{id}/state")]
    Task SetStateAsync(long id, [Body] SetProductStateRequest request);

    [Delete("/api/products/{id}")]
    Task DeleteAsync(long id);

    [Post("/api/products/price")]
    Task SetPriceAsync([Body] SetProductPriceRequest request);

    [Multipart]
    [Post("/api/products/import/preview")]
    Task<ProductImportPreviewDto> PreviewImportAsync(StreamPart file, [Query] string? mapping = null);

    [Post("/api/products/import")]
    Task<ImportResultDto> ImportAsync([Body] ImportProductsRequest request);

    [Get("/api/products/import/template")]
    Task<Stream> GetImportTemplateAsync();

    [Get("/api/products/{productId}/variants")]
    Task<List<VariantDto>> GetVariantsAsync(long productId);

    [Post("/api/products/{productId}/variants")]
    Task<long> CreateVariantAsync(long productId, [Body] CreateVariantRequest request);

    [Put("/api/products/variants/{id}")]
    Task UpdateVariantAsync(long id, [Body] UpdateVariantRequest request);

    [Delete("/api/products/variants/{id}")]
    Task DeleteVariantAsync(long id);

    [Get("/api/products/{productId}/packs")]
    Task<List<ProductPackDto>> GetPacksAsync(long productId);

    [Post("/api/products/{productId}/packs")]
    Task<long> CreatePackAsync(long productId, [Body] SaveProductPackRequest request);

    [Put("/api/products/packs/{id}")]
    Task UpdatePackAsync(long id, [Body] SaveProductPackRequest request);

    [Delete("/api/products/packs/{id}")]
    Task DeletePackAsync(long id);
}
