using Cartex.Shared.Models.Stocks;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStocksApi
{
    [Get("/api/stocks")]
    Task<List<StockDto>> GetAllAsync([Query] long warehouseId, [Query] string? search = null);

    [Get("/api/stocks/on-hand")]
    Task<List<StockOnHandDto>> GetOnHandAsync([Query] long warehouseId);

    [Get("/api/stocks/expiring")]
    Task<List<ExpiringStockDto>> GetExpiringAsync([Query] int withinDays = 30);
}
