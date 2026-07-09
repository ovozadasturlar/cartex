using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISalesApi
{
    [Get("/api/sales")]
    Task<List<SaleDto>> GetAllAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);

    [Get("/api/sales")]
    Task<IApiResponse<List<SaleDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/sales/totals")]
    Task<SalesTotalsDto> GetTotalsAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] string? search = null);

    [Post("/api/sales")]
    Task<CreateSaleResult> CreateAsync([Body] CreateSaleRequest request);

    [Post("/api/sales/{id}/return")]
    Task ReturnAsync(long id, [Body] ReturnSaleRequest request);
}
