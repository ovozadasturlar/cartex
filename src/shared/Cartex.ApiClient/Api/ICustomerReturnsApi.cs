using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICustomerReturnsApi
{
    [Get("/api/customer-returns")]
    Task<List<CustomerReturnListDto>> GetAsync(
        [Query] long? customerId = null,
        [Query] long? saleId = null,
        [Query] DateOnly? fromDate = null,
        [Query] DateOnly? toDate = null,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Get("/api/customer-returns/{id}")]
    Task<CustomerReturnDocumentDto> GetByIdAsync(long id);

    [Post("/api/customer-returns")]
    Task<CustomerReturnCreatedDto> CreateAsync([Body] CreateCustomerReturnRequest request);
}
