using Cartex.Shared.Models.Customers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICustomerRefundsApi
{
    [Get("/api/customer-refunds")]
    Task<List<CustomerRefundListDto>> GetAsync(
        [Query] long? customerId = null,
        [Query] DateOnly? fromDate = null,
        [Query] DateOnly? toDate = null,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Get("/api/customer-refunds/{id}")]
    Task<CustomerRefundDocumentDto> GetByIdAsync(long id);

    [Post("/api/customer-refunds")]
    Task<CustomerRefundCreatedDto> CreateAsync([Body] CreateCustomerRefundRequest request);
}
