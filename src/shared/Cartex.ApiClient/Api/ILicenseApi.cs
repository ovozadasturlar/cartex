using Cartex.Shared.Models.Licensing;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ILicenseApi
{
    [Get("/api/license")]
    Task<LicenseStatusDto> GetAsync();

    [Get("/api/license/options")]
    Task<LicenseOptionsDto> GetOptionsAsync();

    [Put("/api/license")]
    Task UpdateAsync([Body] UpdateLicenseRequest request);
}
