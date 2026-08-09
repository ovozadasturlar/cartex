using Cartex.Shared.Models.Partners;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IPartnersApi
{
    [Get("/api/partners")]
    Task<List<PartnerDto>> GetAsync(
        [Query] string? search = null,
        [Query] bool? isEnabled = null,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Post("/api/partners")]
    Task<long> CreateAsync([Body] CreatePartnerRequest request);

    [Put("/api/partners/{id}")]
    Task UpdateAsync(long id, [Body] UpdatePartnerRequest request);

    [Get("/api/partners/{partnerId}/rewards")]
    Task<List<PartnerRewardEntryDto>> GetRewardsAsync(
        long partnerId,
        [Query] int page = 1,
        [Query] int pageSize = 50);

    [Get("/api/partners/roles")]
    Task<List<ParticipantRoleDto>> GetRolesAsync([Query] bool includeDisabled = false);

    [Put("/api/partners/roles")]
    Task<long> SaveRoleAsync([Body] SaveParticipantRoleRequest request);

    [Get("/api/partners/programs")]
    Task<List<PartnerProgramDto>> GetProgramsAsync([Query] bool includeDisabled = false);

    [Put("/api/partners/programs")]
    Task<long> SaveProgramAsync([Body] SavePartnerProgramRequest request);

    [Get("/api/partners/ranking")]
    Task<List<PartnerRankingDto>> GetRankingAsync(
        [Query] DateTime? from = null,
        [Query] DateTime? to = null,
        [Query] int take = 100);

    [Post("/api/partners/{partnerId}/redemptions")]
    Task<PartnerRedemptionCreatedDto> RedeemAsync(long partnerId, [Body] PartnerRedemptionRequest request);

    [Post("/api/partners/{partnerId}/adjustments")]
    Task<PartnerRewardAdjustmentResult> AdjustAsync(
        long partnerId, [Body] PartnerRewardAdjustmentRequest request);
}
