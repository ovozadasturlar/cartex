using Cartex.Shared.Models.Transactions;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ITransactionsApi
{
    [Get("/api/transactions")]
    Task<List<TransactionDto>> GetAllAsync([Query] DateTime? fromDate = null, [Query] DateTime? toDate = null,
        [Query] string? operationType = null);

    [Get("/api/transactions")]
    Task<IApiResponse<List<TransactionDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/transactions/totals")]
    Task<TransactionsTotalsDto> GetTotalsAsync([Query] string? search = null, [Query] DateTime? fromDate = null,
        [Query] DateTime? toDate = null, [Query] string? operationType = null);
}
