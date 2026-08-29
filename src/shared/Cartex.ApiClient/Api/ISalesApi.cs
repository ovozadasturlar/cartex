using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISalesApi
{
    [Get("/api/sales")]
    Task<List<SaleDto>> GetAllAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null);

    [Get("/api/sales")]
    Task<IApiResponse<List<SaleDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/sales/list")]
    Task<IApiResponse<List<SaleListDto>>> QueryListAsync([Query] IDictionary<string, object> query);

    [Get("/api/sales/totals")]
    Task<SalesTotalsDto> GetTotalsAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] string? search = null, [Query] long? customerId = null);

    [Get("/api/sales/totals/daily")]
    Task<List<DailySalesPointDto>> GetDailyTotalsAsync([Query] long? warehouseId = null, [Query] DateTime? fromDate = null, [Query] DateTime? toDate = null, [Query] int? tzOffsetMinutes = null);

    [Get("/api/sales/{id}")]
    Task<SaleDetailDto> GetByIdAsync(long id);

    [Post("/api/sales")]
    Task<CreateSaleResult> CreateAsync([Body] CreateSaleRequest request);

    [Post("/api/sales/{id}/void")]
    Task VoidAsync(long id, [Body] VoidSaleRequest request);

    [Get("/api/sales/variant-prices/{variantId}")]
    Task<List<VariantSalePriceDto>> GetVariantPricesAsync(long variantId, [Query] long? customerId = null, [Query] int take = 10);

    [Post("/api/sales/{id}/resend-receipt")]
    Task ResendReceiptAsync(long id);

    [Post("/api/sales/{id}/receipt-sms")]
    Task<ReceiptSmsResultDto> SendReceiptSmsAsync(long id, [Body] SendReceiptSmsRequest request);

    [Get("/api/sales/{id}/receipt-sms-preview")]
    Task<ReceiptSmsPreviewDto> GetReceiptSmsPreviewAsync(long id);

    [Put("/api/sales/{id}/customer/{customerId}")]
    Task AssignCustomerAsync(long id, long customerId);
}
