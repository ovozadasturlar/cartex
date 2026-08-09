using Cartex.Shared.Models.Customers;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ICustomerPaymentsApi
{
    [Get("/api/customer-payments")]
    Task<List<CustomerPaymentListDto>> GetAsync(
        [Query] long? customerId = null,
        [Query] long? tradeCaseId = null,
        [Query] DateOnly? fromDate = null,
        [Query] DateOnly? toDate = null,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Get("/api/customer-payments/{id}")]
    Task<CustomerPaymentDocumentDto> GetByIdAsync(long id);

    [Post("/api/customer-payments")]
    Task<CustomerPaymentCreatedDto> CreateAsync([Body] CreateCustomerPaymentRequest request);
}
