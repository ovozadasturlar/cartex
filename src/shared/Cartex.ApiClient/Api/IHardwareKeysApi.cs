using Cartex.Shared.Models.Auth;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IHardwareKeysApi
{
    [Post("/api/hardware-keys")]
    Task<HardwareKeyResult> GenerateAsync([Body] GenerateHardwareKeyRequest request);
}
