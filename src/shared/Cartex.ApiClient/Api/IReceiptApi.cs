using Cartex.Shared.Models.Sales;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IReceiptApi
{
    [Get("/r/{token}")]
    Task<ReceiptDto> GetAsync(string token);

    [Get("/r/{token}/pdf")]
    Task<HttpContent> GetPdfAsync(string token, [Query] string size);

    [Get("/r/{token}/print-pages")]
    Task<HttpContent> GetPrintImagesAsync(
        string token,
        [Query] string size,
        [Query] string orientation,
        [Query] long? printJobId = null,
        [Query] bool monochrome = false);
}
