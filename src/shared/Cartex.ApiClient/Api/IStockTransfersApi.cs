using Cartex.Shared.Models.StockTransfers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStockTransfersApi
{
    [Get("/api/stock-transfers")]
    Task<List<StockTransferDto>> GetAllAsync([Query] long? warehouseId = null);

    [Post("/api/stock-transfers")]
    Task<long> CreateAsync([Body] CreateStockTransferRequest request);

    [Put("/api/stock-transfers/{id}/receive")]
    Task ReceiveAsync(long id);
}
