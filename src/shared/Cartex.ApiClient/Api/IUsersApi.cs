using Cartex.Shared.Models.Users;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IUsersApi
{
    [Get("/api/users")]
    Task<List<UserDto>> GetAllAsync([Query] long? shopId = null);

    [Post("/api/users")]
    Task<long> CreateAsync([Body] CreateUserRequest request);

    [Put("/api/users/{id}")]
    Task UpdateAsync(long id, [Body] UpdateUserRequest request);
}
