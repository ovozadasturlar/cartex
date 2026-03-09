using Cartex.Shared.Models.Barcodes;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IBarcodesApi
{
    [Post("/api/barcodes")]
    Task<long> CreateAsync([Body] CreateBarcodeRequest request);
}
