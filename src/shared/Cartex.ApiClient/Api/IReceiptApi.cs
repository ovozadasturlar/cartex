using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IReceiptApi
{
    [Get("/r/{token}")]
    Task<ReceiptDto> GetAsync(string token);

    [Get("/r/{token}/pdf")]
    Task<HttpContent> GetPdfAsync(string token, [Query] string size);
}
