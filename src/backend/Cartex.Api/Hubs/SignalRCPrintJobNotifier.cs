using Cartex.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

public sealed class SignalRCPrintJobNotifier(IHubContext<PrintingHub> hub) : IPrintJobNotifier
{
    public Task NotifyJobAvailableAsync(string deviceId, long jobId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(Group(deviceId)).SendAsync("PrintJobAvailable", jobId, cancellationToken);

    public static string Group(string deviceId) => $"print:{deviceId}";
}

