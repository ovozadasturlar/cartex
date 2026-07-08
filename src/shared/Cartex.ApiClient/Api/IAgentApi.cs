using Cartex.Shared.Models.Agents;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAgentApi
{
    [Get("/api/agent/bootstrap")]
    Task<AgentBootstrapDto> BootstrapAsync();
}
