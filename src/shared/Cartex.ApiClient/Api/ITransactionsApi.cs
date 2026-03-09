using Cartex.Shared.Models.Transactions;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ITransactionsApi
{
    [Get("/api/transactions")]
    Task<List<TransactionDto>> GetAllAsync([Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);
}
