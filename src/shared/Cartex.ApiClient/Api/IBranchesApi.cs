using Cartex.Shared.Models.Branches;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IBranchesApi
{
    [Get("/api/branches")]
    Task<List<BranchDto>> GetAllAsync();

    [Post("/api/branches")]
    Task<long> CreateAsync([Body] CreateBranchRequest request);

    [Put("/api/branches/{id}")]
    Task UpdateAsync(long id, [Body] UpdateBranchRequest request);

    [Get("/api/branches/{branchId}/catalog")]
    Task<BranchCatalogPageDto> GetCatalogAsync(long branchId, [Query] string? search = null, [Query] int page = 1, [Query] int pageSize = 80);

    [Put("/api/branches/{branchId}/catalog/{variantId}")]
    Task SetCatalogVisibilityAsync(long branchId, long variantId, [Body] SetBranchCatalogVisibilityRequest request);
}
