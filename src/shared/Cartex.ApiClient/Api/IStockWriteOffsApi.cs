using Cartex.Shared.Models.Stocks;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IStockWriteOffsApi
{
    [Get("/api/stock-write-offs")]
    Task<IApiResponse<List<StockWriteOffDto>>> QueryAsync([Query] DateOnly? from = null, [Query] DateOnly? to = null,
        [Query] long? warehouseId = null, [Query] string? reason = null, [Query] int page = 1, [Query] int pageSize = 50);

    [Get("/api/stock-write-offs/balances")]
    Task<List<WriteOffBalanceDto>> GetBalancesAsync([Query] long? warehouseId = null, [Query] long? variantId = null);

    [Get("/api/stock-write-offs/batches")]
    Task<List<WriteOffBatchDto>> GetBatchesAsync([Query] long warehouseId, [Query] long variantId);

    [Post("/api/stock-write-offs")]
    Task<StockWriteOffCreatedDto> CreateAsync([Body] CreateStockWriteOffRequest request);

    [Post("/api/stock-write-offs/{id}/reverse")]
    Task<StockWriteOffCreatedDto> ReverseAsync(long id, [Body] ReverseStockWriteOffRequest request);
}
