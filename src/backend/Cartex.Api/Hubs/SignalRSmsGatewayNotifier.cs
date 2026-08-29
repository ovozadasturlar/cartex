using Cartex.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

public sealed class SignalRSmsGatewayNotifier(IHubContext<SmsGatewayHub> hub) : ISmsGatewayNotifier
{
    public Task NotifyJobAvailableAsync(string deviceId, int simSlot, long jobId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(HubChannels.SmsGateway(deviceId, simSlot)).SendAsync("SmsJobAvailable", jobId, cancellationToken);
}
