using Cartex.Shared.Models.Barcodes;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IBarcodesApi
{
    [Get("/api/barcodes/by-variant/{variantId}")]
    Task<List<BarcodeDto>> GetByVariantAsync(long variantId);

    [Post("/api/barcodes")]
    Task<long> CreateAsync([Body] CreateBarcodeRequest request);

    [Delete("/api/barcodes/{id}")]
    Task DeleteAsync(long id);

    [Post("/api/barcodes/generate/{variantId}")]
    Task<string> GenerateAsync(long variantId, [Query] decimal packQty = 1);
}
