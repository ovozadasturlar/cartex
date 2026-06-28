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
}
