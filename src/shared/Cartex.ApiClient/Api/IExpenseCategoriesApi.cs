using Cartex.Shared.Models.ExpenseCategories;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IExpenseCategoriesApi
{
    [Get("/api/expense-categories")]
    Task<List<ExpenseCategoryDto>> GetAllAsync();

    [Post("/api/expense-categories")]
    Task<long> CreateAsync([Body] CreateExpenseCategoryRequest request);

    [Put("/api/expense-categories/{id}")]
    Task UpdateAsync(long id, [Body] UpdateExpenseCategoryRequest request);
}
