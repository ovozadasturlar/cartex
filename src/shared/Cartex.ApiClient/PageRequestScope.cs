namespace Cartex.ApiClient;

public sealed class PageRequestScope
{
    private static readonly AsyncLocal<bool> PageScoped = new();

    private CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public static bool IsPageLoad => PageScoped.Value;

    public IDisposable BeginPageRequest()
    {
        PageScoped.Value = true;
        return new Reset();
    }

    public static IDisposable Detach()
    {
        var previous = PageScoped.Value;
        PageScoped.Value = false;
        return new Restore(previous);
    }

    public void CancelPending()
    {
        var previous = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }

    private sealed class Reset : IDisposable
    {
        public void Dispose() => PageScoped.Value = false;
    }

    private sealed class Restore(bool previous) : IDisposable
    {
        public void Dispose() => PageScoped.Value = previous;
    }
}

public sealed class PageRequestScopeHandler(PageRequestScope scope) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || !PageRequestScope.IsPageLoad)
            return await base.SendAsync(request, cancellationToken);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, scope.Token);
        return await base.SendAsync(request, linked.Token);
    }
}
