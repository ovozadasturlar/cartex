using Cartex.Shared.Models.Transactions;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ITransactionsApi
{
    [Get("/api/transactions")]
    Task<List<TransactionDto>> GetAllAsync([Query] DateTime? fromDate = null, [Query] DateTime? toDate = null,
        [Query] string? operationType = null);

    [Get("/api/transactions")]
    Task<IApiResponse<List<TransactionDto>>> GetPagedAsync([Query] int page, [Query] int pageSize,
        [Query] string? sortBy = null, [Query] bool descending = false, [Query] string? search = null,
        [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] string? operationType = null);

    [Get("/api/transactions/totals")]
    Task<TransactionsTotalsDto> GetTotalsAsync([Query] string? search = null, [Query] DateTime? fromDate = null,
        [Query] DateTime? toDate = null, [Query] string? operationType = null);
}
