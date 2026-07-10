using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface ISessionsApi
{
    [Get("/api/auth/sessions")]
    Task<IReadOnlyList<DeviceSessionDto>> GetSessionsAsync([Query] bool all = false);

    [Delete("/api/auth/sessions/{id}")]
    Task RevokeSessionAsync(long id);

    [Post("/api/auth/qr/approve")]
    Task ApproveQrAsync([Body] ApproveQrLoginRequest request);
}
