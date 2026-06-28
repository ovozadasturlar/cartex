using Cartex.Shared.Models.Customers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICustomersApi
{
    [Get("/api/customers")]
    Task<List<CustomerDto>> GetAllAsync([Query] string? search = null);

    [Post("/api/customers")]
    Task<long> CreateAsync([Body] CreateCustomerRequest request);

    [Get("/api/customers/{id}/ledger")]
    Task<List<CustomerLedgerEntryDto>> GetLedgerAsync(long id);

    [Post("/api/customers/{id}/repay-debt")]
    Task RepayDebtAsync(long id, [Body] RepayDebtRequest request);
}
