using Cartex.Shared.Models.Rates;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IRatesApi
{
    [Get("/api/rates")]
    Task<List<RateDto>> GetCurrentAsync();

    [Get("/api/rates/{code}/history")]
    Task<List<RateDto>> GetHistoryAsync(string code);

    [Post("/api/rates")]
    Task<long> SetAsync([Body] SetRateRequest request);
}
