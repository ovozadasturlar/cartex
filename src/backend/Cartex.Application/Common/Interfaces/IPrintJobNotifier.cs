using Cartex.Shared.Models.Printing;

namespace Cartex.Application.Common.Interfaces;

public interface IPrintJobNotifier
{
    Task NotifyJobAvailableAsync(string deviceId, long jobId, CancellationToken cancellationToken = default);
    Task NotifyJobStatusChangedAsync(string deviceId, PrintJobStatusUpdate update, CancellationToken cancellationToken = default);
}
