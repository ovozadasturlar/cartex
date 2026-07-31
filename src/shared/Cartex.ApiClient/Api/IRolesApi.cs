using Cartex.Shared.Models.Roles;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IRolesApi
{
    [Get("/api/roles")]
    Task<List<RoleDto>> GetAllAsync();

    [Post("/api/roles")]
    Task<long> CreateAsync([Body] CreateRoleRequest request);

    [Put("/api/roles/{id}")]
    Task UpdateAsync(long id, [Body] UpdateRoleRequest request);

    [Put("/api/roles/{id}/permissions")]
    Task AssignPermissionsAsync(long id, [Body] AssignPermissionsRequest request);

    [Put("/api/roles/{id}/active")]
    Task SetActiveAsync(long id, [Body] SetRoleActiveRequest request);

    [Delete("/api/roles/{id}")]
    Task DeleteAsync(long id);
}
