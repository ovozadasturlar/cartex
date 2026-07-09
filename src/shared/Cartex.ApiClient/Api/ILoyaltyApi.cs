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

    [Get("/api/loyalty/discounts")]
    Task<List<DiscountRuleDto>> GetDiscountRulesAsync();

    [Post("/api/loyalty/discounts")]
    Task<long> SaveDiscountRuleAsync([Body] SaveDiscountRuleRequest request);

    [Delete("/api/loyalty/discounts/{id}")]
    Task DeleteDiscountRuleAsync(long id);

    [Post("/api/loyalty/discount-preview")]
    Task<PreviewDiscountResultDto> PreviewDiscountAsync([Body] PreviewDiscountRequest request);
}
