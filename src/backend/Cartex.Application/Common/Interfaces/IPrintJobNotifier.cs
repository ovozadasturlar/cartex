namespace Cartex.Application.Common.Interfaces;

public interface IPrintJobNotifier
{
    Task NotifyJobAvailableAsync(string deviceId, long jobId, CancellationToken cancellationToken = default);
}

