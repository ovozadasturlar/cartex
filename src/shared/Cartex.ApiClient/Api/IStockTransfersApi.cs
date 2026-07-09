using Cartex.Shared.Models.StockTransfers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStockTransfersApi
{
    [Get("/api/stock-transfers")]
    Task<List<StockTransferDto>> GetAllAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] long? toWarehouseId = null);

    [Get("/api/stock-transfers")]
    Task<IApiResponse<List<StockTransferDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/stock-transfers/totals")]
    Task<StockTransfersTotalsDto> GetTotalsAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] string? search = null);

    [Post("/api/stock-transfers")]
    Task<long> CreateAsync([Body] CreateStockTransferRequest request);

    [Put("/api/stock-transfers/{id}/receive")]
    Task ReceiveAsync(long id);
}
