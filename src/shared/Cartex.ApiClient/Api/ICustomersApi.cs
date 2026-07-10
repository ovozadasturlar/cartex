using Cartex.Shared.Models.Customers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICustomersApi
{
    [Get("/api/customers")]
    Task<List<CustomerDto>> GetAllAsync([Query] string? search = null);

    [Get("/api/customers")]
    Task<IApiResponse<List<CustomerDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/customers/by-card/{code}")]
    Task<CustomerDto?> GetByCardAsync(string code);

    [Get("/api/customers/totals")]
    Task<CustomerTotalsDto> GetTotalsAsync([Query] string? search = null);

    [Post("/api/customers")]
    Task<long> CreateAsync([Body] CreateCustomerRequest request);

    [Put("/api/customers/{id}")]
    Task UpdateAsync(long id, [Body] UpdateCustomerRequest request);

    [Get("/api/customers/{id}/ledger")]
    Task<IApiResponse<List<CustomerLedgerEntryDto>>> GetLedgerAsync(long id, [Query] int page = 1, [Query] int pageSize = 50, CancellationToken cancellationToken = default);

    [Post("/api/customers/{id}/repay-debt")]
    Task RepayDebtAsync(long id, [Body] RepayDebtRequest request);

    [Post("/api/customers/{id}/bonus")]
    Task GiveCustomerBonusAsync(long id, [Body] GiveCustomerBonusRequest request);

    [Get("/api/customers/{id}")]
    Task<CustomerDto> GetByIdAsync(long id);

    [Post("/api/customers/{id}/message")]
    Task SendMessageAsync(long id, [Body] SendCustomerMessageRequest request);
}
