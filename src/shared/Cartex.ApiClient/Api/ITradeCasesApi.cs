using Cartex.Shared.Models.TradeCases;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ITradeCasesApi
{
    [Get("/api/trade-cases")]
    Task<List<TradeCaseListDto>> GetAsync(
        [Query] long? customerId = null,
        [Query] long? warehouseId = null,
        [Query] string? status = null,
        [Query] string? search = null,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Get("/api/trade-cases/{id}")]
    Task<TradeCaseDetailDto> GetByIdAsync(long id);

    [Post("/api/trade-cases")]
    Task<TradeCaseCreatedDto> CreateAsync([Body] CreateTradeCaseRequest request);

    [Put("/api/trade-cases/{id}")]
    Task UpdateAsync(long id, [Body] UpdateTradeCaseRequest request);

    [Post("/api/trade-cases/{id}/close")]
    Task CloseAsync(long id, [Body] ChangeTradeCaseStatusRequest request);

    [Post("/api/trade-cases/{id}/cancel")]
    Task CancelAsync(long id, [Body] ChangeTradeCaseStatusRequest request);

    [Put("/api/trade-cases/{id}/sales/{saleId}")]
    Task LinkSaleAsync(long id, long saleId);

    [Delete("/api/trade-cases/{id}/sales/{saleId}")]
    Task UnlinkSaleAsync(long id, long saleId);

    [Get("/api/trade-cases/issues/{issueId}/print")]
    Task<GoodsIssuePrintDto> GetIssuePrintAsync(long issueId);

    [Post("/api/trade-cases/{id}/issues")]
    Task<GoodsIssueCreatedDto> IssueAsync(long id, [Body] CreateGoodsIssueRequest request);

    [Post("/api/trade-cases/{id}/returns")]
    Task<GoodsReturnCreatedDto> ReturnAsync(long id, [Body] CreateGoodsReturnRequest request);

    [Post("/api/trade-cases/{id}/settlements")]
    Task<TradeCaseSettlementCreatedDto> SettleAsync(long id, [Body] SettleTradeCaseRequest request);

    [Get("/api/trade-cases/{id}/statement")]
    Task<TradeCaseStatementDto> StatementAsync(long id, [Query] DateTime? from = null, [Query] DateTime? to = null);

    [Get("/api/trade-cases/{id}/statement/export")]
    Task<HttpContent> ExportStatementAsync(long id, [Query] string format = "pdf", [Query] string mode = "both",
        [Query] DateTime? from = null, [Query] DateTime? to = null);
}
