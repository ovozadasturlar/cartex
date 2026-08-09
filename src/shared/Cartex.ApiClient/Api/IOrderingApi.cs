using Cartex.Shared.Models.Ordering;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IOrderingApi
{
    [Post("/api/ordering/carts")]
    Task<string> SubmitAsync([Body] SubmitCartRequest request);

    [Get("/api/ordering/carts/{code}")]
    Task<CartDto> GetByCodeAsync(string code);

    [Post("/api/ordering/carts/{code}/checkout")]
    Task<long> CheckoutAsync(string code, [Body] CheckoutCartRequest request);

    [Post("/api/ordering/carts/{code}/requeue")]
    Task<RequeueCartResult> RequeueAsync(string code, [Body] RequeueCartRequest request);

    [Get("/api/ordering/carts")]
    Task<List<CartListDto>> GetAllAsync([Query] string? status = null, [Query] long? warehouseId = null, [Query] string? kind = null);

    [Get("/api/ordering/load")]
    Task<List<CartLoadItemDto>> GetLoadAsync([Query] long? warehouseId = null, [Query] string? status = null);

    [Put("/api/ordering/carts/{code}/status")]
    Task UpdateStatusAsync(string code, [Body] UpdateCartStatusRequest request);

    [Put("/api/ordering/carts/{code}/items")]
    Task UpdateItemsAsync(string code, [Body] UpdateCartItemsRequest request);

    [Put("/api/ordering/carts/{code}")]
    Task UpdateAsync(string code, [Body] UpdateCartRequest request);
}
