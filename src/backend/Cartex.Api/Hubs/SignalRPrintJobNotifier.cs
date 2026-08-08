using Cartex.Application.Common.Interfaces;
using Cartex.Shared.Models.Printing;
using Microsoft.AspNetCore.SignalR;

namespace Cartex.Api.Hubs;

public sealed class SignalRPrintJobNotifier(IHubContext<PrintingHub> hub) : IPrintJobNotifier
{
    public Task NotifyJobAvailableAsync(string deviceId, long jobId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(HostGroup(deviceId)).SendAsync("PrintJobAvailable", jobId, cancellationToken);

    public Task NotifyJobStatusChangedAsync(string deviceId, PrintJobStatusUpdate update, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(RequesterGroup(deviceId)).SendAsync("PrintJobStatusChanged", update, cancellationToken);

    public static string HostGroup(string deviceId) => $"print-host:{deviceId}";
    public static string RequesterGroup(string deviceId) => $"print-requester:{deviceId}";
}
