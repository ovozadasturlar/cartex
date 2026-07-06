using Cartex.Shared.Models.Features;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IFeaturesApi
{
    [Get("/api/features")]
    Task<List<FeatureDto>> GetAllAsync();

    [Put("/api/features/{code}")]
    Task SetAsync(string code, [Body] SetFeatureRequest request);

    [Get("/api/features/enabled")]
    Task<List<string>> GetEnabledAsync();
}
