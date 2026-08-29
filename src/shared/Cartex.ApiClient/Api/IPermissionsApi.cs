using Cartex.Shared.Models.Permissions;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IPermissionsApi
{
    [Get("/api/permissions")]
    Task<List<PermissionDto>> GetAllAsync();

    [Get("/api/permissions/bundles")]
    Task<List<PermissionBundleDto>> GetBundlesAsync();

    [Put("/api/permissions/{id}/toggle")]
    Task ToggleAsync(long id, [Body] TogglePermissionRequest request);
}
