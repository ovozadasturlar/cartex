using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request);

    [Post("/api/auth/login-with-key")]
    Task<LoginResponse> LoginWithKeyAsync([Body] LoginWithKeyRequest request);

    [Post("/api/auth/refresh")]
    Task<LoginResponse> RefreshAsync([Body] RefreshRequest request);

    [Post("/api/auth/logout")]
    Task LogoutAsync([Body] LogoutRequest request);
}
