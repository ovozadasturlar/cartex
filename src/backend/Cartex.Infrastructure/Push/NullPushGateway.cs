using Cartex.Application.Common.Interfaces;

namespace Cartex.Infrastructure.Push;

public sealed class NullPushGateway : IPushGateway
{
    public bool IsEnabled => false;

    public Task SendAsync(PushMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
