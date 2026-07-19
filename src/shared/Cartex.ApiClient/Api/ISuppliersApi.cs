using Cartex.Shared.Models.Suppliers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISuppliersApi
{
    [Get("/api/suppliers")]
    Task<List<SupplierDto>> GetAllAsync();

    [Get("/api/suppliers")]
    Task<IApiResponse<List<SupplierDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/suppliers/totals")]
    Task<SupplierTotalsDto> GetTotalsAsync([Query] string? search = null);

    [Get("/api/suppliers/{id}/ledger")]
    Task<IApiResponse<List<SupplierLedgerEntryDto>>> GetLedgerAsync(long id, [Query] int page = 1, [Query] int pageSize = 50, CancellationToken cancellationToken = default);

    [Post("/api/suppliers")]
    Task<long> CreateAsync([Body] CreateSupplierRequest request);

    [Put("/api/suppliers/{id}")]
    Task UpdateAsync(long id, [Body] UpdateSupplierRequest request);

    [Post("/api/suppliers/{id}/pay-debt")]
    Task PayDebtAsync(long id, [Body] PaySupplierDebtRequest request);

    [Get("/api/suppliers/{id}/payments")]
    Task<List<SupplierPaymentDto>> GetPaymentsAsync(long id, [Query(Format = "yyyy-MM-dd")] DateOnly date);
}
