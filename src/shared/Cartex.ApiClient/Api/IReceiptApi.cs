using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IReceiptApi
{
    [Get("/r/{token}")]
    Task<ReceiptDto> GetAsync(string token);
}
