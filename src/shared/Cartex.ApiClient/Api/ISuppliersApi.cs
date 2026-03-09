using Cartex.Shared.Models.Suppliers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISuppliersApi
{
    [Get("/api/suppliers")]
    Task<List<SupplierDto>> GetAllAsync();

    [Post("/api/suppliers")]
    Task<long> CreateAsync([Body] CreateSupplierRequest request);
}
