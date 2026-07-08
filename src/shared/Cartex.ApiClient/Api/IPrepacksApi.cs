using Cartex.Shared.Models.Prepacks;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IPrepacksApi
{
    [Get("/api/prepacks")]
    Task<List<PrepackDto>> GetAllAsync([Query] long warehouseId);

    [Get("/api/prepacks/by-code")]
    Task<PrepackLookupDto> GetByCodeAsync([Query] string code, [Query] long warehouseId);

    [Post("/api/prepacks")]
    Task<List<PrepackLabelDto>> CreateAsync([Body] CreatePrepacksRequest request);

    [Delete("/api/prepacks/{id}")]
    Task CancelAsync(long id);
}
