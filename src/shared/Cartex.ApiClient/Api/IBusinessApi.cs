using Cartex.Shared.Models.Business;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IBusinessApi
{
    [Get("/api/business")]
    Task<BusinessDto> GetAsync();

    [Put("/api/business")]
    Task UpdateAsync([Body] UpdateBusinessRequest request);

    [Post("/api/business/complete-onboarding")]
    Task CompleteOnboardingAsync([Body] CompleteOnboardingRequest request);
}
