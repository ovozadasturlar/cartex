using Cartex.Shared.Models.Warehouses;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IWarehousesApi
{
    [Get("/api/warehouses")]
    Task<List<WarehouseDto>> GetAllAsync([Query] long? shopId = null);

    [Post("/api/warehouses")]
    Task<long> CreateAsync([Body] CreateWarehouseRequest request);
}
