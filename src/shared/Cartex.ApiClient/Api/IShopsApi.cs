using Cartex.Shared.Models.Shops;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IShopsApi
{
    [Get("/api/shops")]
    Task<List<ShopDto>> GetAllAsync();

    [Post("/api/shops")]
    Task<long> CreateAsync([Body] CreateShopRequest request);

    [Put("/api/shops/{id}")]
    Task UpdateAsync(long id, [Body] UpdateShopRequest request);
}
