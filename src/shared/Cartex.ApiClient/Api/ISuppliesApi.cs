using Cartex.Shared.Models.Supplies;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISuppliesApi
{
    [Get("/api/supplies")]
    Task<List<SupplyDto>> GetAllAsync([Query] long? warehouseId = null);

    [Post("/api/supplies")]
    Task<long> CreateAsync([Body] CreateSupplyRequest request);
}
