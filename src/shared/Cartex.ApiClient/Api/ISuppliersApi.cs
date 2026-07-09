using Cartex.Shared.Models.Suppliers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISuppliersApi
{
    [Get("/api/suppliers")]
    Task<List<SupplierDto>> GetAllAsync();

    [Get("/api/suppliers")]
    Task<IApiResponse<List<SupplierDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Post("/api/suppliers")]
    Task<long> CreateAsync([Body] CreateSupplierRequest request);

    [Put("/api/suppliers/{id}")]
    Task UpdateAsync(long id, [Body] UpdateSupplierRequest request);

    [Post("/api/suppliers/{id}/pay-debt")]
    Task PayDebtAsync(long id, [Body] PaySupplierDebtRequest request);
}
