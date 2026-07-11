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

    [Get("/api/rates/currencies")]
    Task<List<CurrencyDto>> GetCurrenciesAsync([Query] bool onlyEnabled = false);

    [Post("/api/rates/currencies")]
    Task<long> CreateCurrencyAsync([Body] CreateCurrencyRequest request);

    [Put("/api/rates/currencies/{code}")]
    Task UpdateCurrencyAsync(string code, [Body] UpdateCurrencyRequest request);

    [Delete("/api/rates/currencies/{code}")]
    Task DeleteCurrencyAsync(string code);
}
