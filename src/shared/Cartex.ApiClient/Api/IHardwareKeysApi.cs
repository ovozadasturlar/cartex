using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IHardwareKeysApi
{
    [Get("/api/hardware-keys")]
    Task<List<HardwareKeyDto>> GetAllAsync();

    [Post("/api/hardware-keys")]
    Task<HardwareKeyResult> GenerateAsync([Body] GenerateHardwareKeyRequest request);

    [Put("/api/hardware-keys/{id}/enabled")]
    Task SetEnabledAsync(long id, [Body] SetHardwareKeyEnabledRequest request);

    [Delete("/api/hardware-keys/{id}")]
    Task RevokeAsync(long id);
}
