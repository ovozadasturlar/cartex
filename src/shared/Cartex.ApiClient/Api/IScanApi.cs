using Cartex.Shared.Models.Scan;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IScanApi
{
    [Get("/api/scan")]
    Task<ScanResultDto> ScanAsync([Query] string code, [Query] long warehouseId, [Query] bool forSale = false);
}
