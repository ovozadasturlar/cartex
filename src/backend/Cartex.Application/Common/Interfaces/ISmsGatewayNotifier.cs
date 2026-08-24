namespace Cartex.Application.Common.Interfaces;

public interface ISmsGatewayNotifier
{
    Task NotifyJobAvailableAsync(string deviceId, int simSlot, long jobId, CancellationToken cancellationToken = default);
}
