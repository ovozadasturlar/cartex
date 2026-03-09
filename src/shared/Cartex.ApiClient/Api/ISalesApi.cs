using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISalesApi
{
    [Get("/api/sales")]
    Task<List<SaleDto>> GetAllAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);

    [Post("/api/sales")]
    Task<long> CreateAsync([Body] CreateSaleRequest request);
}
