using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request);

    [Post("/api/auth/login-with-key")]
    Task<LoginResponse> LoginWithKeyAsync([Body] LoginWithKeyRequest request);

    [Get("/api/auth/qr/enabled")]
    Task<bool> GetQrEnabledAsync();

    [Post("/api/auth/qr/start")]
    Task<QrLoginStartResponse> StartQrAsync();

    [Post("/api/auth/qr/approve")]
    Task ApproveQrAsync([Body] ApproveQrLoginRequest request);

    [Post("/api/auth/qr/poll")]
    Task<IApiResponse<LoginResponse>> PollQrAsync([Body] PollQrLoginRequest request);

    [Post("/api/auth/refresh")]
    Task<LoginResponse> RefreshAsync([Body] RefreshRequest request);

    [Post("/api/auth/logout")]
    Task LogoutAsync([Body] LogoutRequest request);
}
