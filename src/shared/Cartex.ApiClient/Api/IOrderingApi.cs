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
}
