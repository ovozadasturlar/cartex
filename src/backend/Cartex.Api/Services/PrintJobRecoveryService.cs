using Cartex.Application.Common.Messaging;
using Cartex.Application.Printing;

namespace Cartex.Api.Services;

public sealed class PrintJobRecoveryService(IServiceScopeFactory scopeFactory, ILogger<PrintJobRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new RecoverPrintJobsCommand(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Print job recovery failed");
            }
        }
    }
}

