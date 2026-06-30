using Cartex.Shared.Models.Users;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IUsersApi
{
    [Get("/api/users")]
    Task<List<UserDto>> GetAllAsync([Query] long? shopId = null);

    [Get("/api/users")]
    Task<IApiResponse<List<UserDto>>> GetPagedAsync([Query] int page, [Query] int pageSize,
        [Query] string? sortBy = null, [Query] bool descending = false, [Query] string? search = null,
        [Query] long? shopId = null);

    [Post("/api/users")]
    Task<long> CreateAsync([Body] CreateUserRequest request);

    [Put("/api/users/{id}")]
    Task UpdateAsync(long id, [Body] UpdateUserRequest request);
}
