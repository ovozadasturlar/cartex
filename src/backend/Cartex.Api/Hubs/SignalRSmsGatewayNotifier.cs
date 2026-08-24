using Cartex.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

public sealed class SignalRSmsGatewayNotifier(IHubContext<SmsGatewayHub> hub) : ISmsGatewayNotifier
{
    public Task NotifyJobAvailableAsync(string deviceId, int simSlot, long jobId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(Group(deviceId, simSlot)).SendAsync("SmsJobAvailable", jobId, cancellationToken);

    public static string Group(string deviceId, int simSlot) => $"sms-gateway:{deviceId}:{simSlot}";
}
