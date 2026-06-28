using Cartex.Shared.Models.Units;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IUnitsApi
{
    [Get("/api/units")]
    Task<List<UnitDto>> GetAllAsync();

    [Post("/api/units")]
    Task<long> CreateAsync([Body] CreateUnitRequest request);

    [Put("/api/units/{id}")]
    Task UpdateAsync(long id, [Body] UpdateUnitRequest request);
}
