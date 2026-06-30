using Cartex.Shared.Models.Loyalty;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ILoyaltyApi
{
    [Get("/api/loyalty")]
    Task<LoyaltyProgramDto> GetAsync();

    [Put("/api/loyalty")]
    Task UpdateAsync([Body] UpdateLoyaltyProgramRequest request);

    [Post("/api/loyalty/rules")]
    Task<long> CreateRuleAsync([Body] CreateCashbackRuleRequest request);

    [Put("/api/loyalty/rules/{id}")]
    Task UpdateRuleAsync(long id, [Body] UpdateCashbackRuleRequest request);

    [Delete("/api/loyalty/rules/{id}")]
    Task DeleteRuleAsync(long id);
}
