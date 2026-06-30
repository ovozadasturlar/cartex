using Cartex.Shared.Models.Stocks;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStocksApi
{
    [Get("/api/stocks")]
    Task<List<StockDto>> GetAllAsync([Query] long warehouseId, [Query] string? search = null);

    [Get("/api/stocks/on-hand")]
    Task<StockOnHandPageDto> GetOnHandAsync([Query] long warehouseId, [Query] long? categoryId = null,
        [Query] string? search = null, [Query] int page = 1, [Query] int pageSize = 50);

    [Get("/api/stocks/expiring")]
    Task<List<ExpiringStockDto>> GetExpiringAsync([Query] int withinDays = 30);

    [Get("/api/stocks/low-stock")]
    Task<List<LowStockDto>> GetLowStockAsync([Query] long warehouseId);

    [Post("/api/stocks/adjust")]
    Task AdjustAsync([Body] AdjustStockRequest request);
}
