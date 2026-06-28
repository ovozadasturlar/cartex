using Cartex.Shared.Models.Categories;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICategoriesApi
{
    [Get("/api/categories")]
    Task<List<CategoryDto>> GetAllAsync();

    [Post("/api/categories")]
    Task<long> CreateAsync([Body] CreateCategoryRequest request);

    [Put("/api/categories/{id}")]
    Task UpdateAsync(long id, [Body] UpdateCategoryRequest request);
}
