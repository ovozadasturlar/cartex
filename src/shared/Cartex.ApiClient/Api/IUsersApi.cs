using Cartex.Shared.Models.Users;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IUsersApi
{
    [Get("/api/users")]
    Task<List<UserDto>> GetAllAsync([Query] long? shopId = null);

    [Get("/api/users")]
    Task<IApiResponse<List<UserDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Post("/api/users")]
    Task<long> CreateAsync([Body] CreateUserRequest request);

    [Put("/api/users/{id}")]
    Task UpdateAsync(long id, [Body] UpdateUserRequest request);

    [Delete("/api/users/{id}")]
    Task DeleteAsync(long id);
}
