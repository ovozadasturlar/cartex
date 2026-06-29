using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request);

    [Post("/api/auth/login-with-key")]
    Task<LoginResponse> LoginWithKeyAsync([Body] LoginWithKeyRequest request);
}
