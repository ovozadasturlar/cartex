using Cartex.Shared.Models.Supplies;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISuppliesApi
{
    [Get("/api/supplies")]
    Task<List<SupplyDto>> GetAllAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] long? supplierId = null);

    [Get("/api/supplies")]
    Task<IApiResponse<List<SupplyDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/supplies/totals")]
    Task<SuppliesTotalsDto> GetTotalsAsync([Query] string? search = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] long? supplierId = null);

    [Get("/api/supplies/{id}")]
    Task<SupplyDetailDto> GetByIdAsync(long id);

    [Post("/api/supplies")]
    Task<long> CreateAsync([Body] CreateSupplyRequest request);

    [Post("/api/supplies/{id}/attach-payments")]
    Task AttachPaymentsAsync(long id, [Body] AttachSupplierPaymentsRequest request);

    [Delete("/api/supplies/{id}")]
    Task DeleteAsync(long id);
}
