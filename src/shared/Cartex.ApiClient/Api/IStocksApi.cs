using Cartex.Shared.Models.Stocks;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStocksApi
{
    [Get("/api/stocks")]
    Task<List<StockDto>> GetAllAsync([Query] long warehouseId, [Query] string? search = null);
}
