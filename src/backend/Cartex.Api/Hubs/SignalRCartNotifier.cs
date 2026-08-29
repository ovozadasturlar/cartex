using Cartex.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

public sealed class SignalRCartNotifier(IHubContext<OrderingHub> hub, ILogger<SignalRCartNotifier> logger) : ICartNotifier
{
    public async Task CartsChangedAsync(long branchId, string kind, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(HubChannels.CartFeed(branchId)).SendAsync("CartsChanged", kind, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Unable to notify SignalR clients that carts have changed.");
        }
    }
}
