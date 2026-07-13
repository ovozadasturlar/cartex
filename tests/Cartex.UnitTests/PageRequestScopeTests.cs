using Cartex.ApiClient;
using Xunit;

namespace Cartex.UnitTests;

public class PageRequestScopeTests
{
    /// <summary>So'rovni ushlab turadi va bekor qilinishini kutadi.</summary>
    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage();
        }
    }

    private static (HttpClient Client, BlockingHandler Inner) Build(PageRequestScope scope)
    {
        var inner = new BlockingHandler();
        var handler = new PageRequestScopeHandler(scope) { InnerHandler = inner };
        return (new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, inner);
    }

    [Fact]
    public async Task PageGet_IsCancelled_WhenNavigatingAway()
    {
        var scope = new PageRequestScope();
        var (client, inner) = Build(scope);

        Task<HttpResponseMessage> request;
        using (scope.BeginPageRequest())
            request = client.GetAsync("/products");

        await inner.Started.Task;
        scope.CancelPending();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public async Task BackgroundGet_IsNotCancelled()
    {
        var scope = new PageRequestScope();
        var (client, inner) = Build(scope);

        // Sahifa doirasidan tashqarida — fon sinxronizatsiyasi kabi.
        var request = client.GetAsync("/offline/snapshot");
        await inner.Started.Task;
        scope.CancelPending();

        Assert.False(request.IsCompleted);
    }

    [Fact]
    public async Task PagePost_IsNotCancelled()
    {
        var scope = new PageRequestScope();
        var (client, inner) = Build(scope);

        Task<HttpResponseMessage> request;
        using (scope.BeginPageRequest())
            request = client.PostAsync("/sales", new StringContent("{}"));

        await inner.Started.Task;
        scope.CancelPending();

        Assert.False(request.IsCompleted);
    }

    [Fact]
    public async Task NewPageGet_SurvivesPreviousCancellation()
    {
        var scope = new PageRequestScope();
        var (client, inner) = Build(scope);

        scope.CancelPending();

        Task<HttpResponseMessage> request;
        using (scope.BeginPageRequest())
            request = client.GetAsync("/supplies");

        await inner.Started.Task;
        Assert.False(request.IsCompleted);
    }
}
