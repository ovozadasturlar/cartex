using Cartex.Shared.Models.Loyalty;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IManufacturersApi
{
    [Get("/api/manufacturers")]
    Task<List<ManufacturerDto>> GetAllAsync();

    [Post("/api/manufacturers")]
    Task<long> CreateAsync([Body] SaveManufacturerRequest request);

    [Put("/api/manufacturers/{id}")]
    Task UpdateAsync(long id, [Body] SaveManufacturerRequest request);

    [Delete("/api/manufacturers/{id}")]
    Task DeleteAsync(long id);
}
