using Cartex.Shared.Models.Categories;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICategoriesApi
{
    [Get("/api/categories")]
    Task<List<CategoryDto>> GetAllAsync();

    [Get("/api/categories")]
    Task<List<CategoryDto>> SearchAsync([AliasAs("Search")] string search);

    [Post("/api/categories")]
    Task<long> CreateAsync([Body] CreateCategoryRequest request);

    [Put("/api/categories/{id}")]
    Task UpdateAsync(long id, [Body] UpdateCategoryRequest request);

    [Put("/api/categories/{id}/move")]
    Task MoveAsync(long id, [Body] MoveCategoryRequest request);

    [Post("/api/categories/{id}/merge")]
    Task<int> MergeAsync(long id, [Body] MergeCategoryRequest request);
}
