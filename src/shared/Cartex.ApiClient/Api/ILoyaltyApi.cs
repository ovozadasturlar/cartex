using Cartex.Shared.Models.Loyalty;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ILoyaltyApi
{
    [Get("/api/loyalty")]
    Task<LoyaltyProgramDto> GetAsync();

    [Put("/api/loyalty")]
    Task UpdateAsync([Body] UpdateLoyaltyProgramRequest request);
}
