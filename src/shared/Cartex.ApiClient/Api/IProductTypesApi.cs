using Cartex.Shared.Models.Products;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IProductTypesApi
{
    [Get("/api/product-types")]
    Task<List<ProductTypeDto>> GetAllAsync();

    [Post("/api/product-types")]
    Task<long> CreateAsync([Body] CreateProductTypeRequest request);

    [Put("/api/product-types/{id}")]
    Task UpdateAsync(long id, [Body] UpdateProductTypeRequest request);
}
