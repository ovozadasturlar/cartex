using System.Net;
using Cartex.ApiClient;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;
using Cartex.UI.Services;
using Refit;
using Xunit;

namespace Cartex.UnitTests;

public sealed class AuthTokenHandlerTests
{
    [Fact]
    public async Task WP11_401_refreshes_and_retries_without_ending_session()
    {
        var transport = new SequenceHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        var unauthorized = 0;
        var refreshes = 0;
        using var client = CreateClient(
            transport,
            () => "old-token",
            _ =>
            {
                refreshes++;
                return Task.FromResult<string?>("new-token");
            },
            () => unauthorized++);

        using var response = await client.GetAsync(
            "https://cartex.test/catalog", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, refreshes);
        Assert.Equal(0, unauthorized);
        Assert.Equal(["old-token", "new-token"], transport.Requests.Select(request => request.Token));
    }

    [Fact]
    public async Task WP11_second_401_ends_session_once()
    {
        var transport = new SequenceHandler(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);
        var unauthorized = 0;
        using var client = CreateClient(
            transport,
            () => "old-token",
            _ => Task.FromResult<string?>("new-token"),
            () => unauthorized++);

        using var response = await client.GetAsync(
            "https://cartex.test/catalog", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, unauthorized);
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task WP11_request_without_token_is_not_retried()
    {
        var transport = new SequenceHandler(HttpStatusCode.Unauthorized);
        var unauthorized = 0;
        var refreshes = 0;
        using var client = CreateClient(
            transport,
            () => null,
            _ =>
            {
                refreshes++;
                return Task.FromResult<string?>("new-token");
            },
            () => unauthorized++);

        using var response = await client.GetAsync(
            "https://cartex.test/catalog", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, refreshes);
        Assert.Equal(0, unauthorized);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task WP11_post_body_is_preserved_when_request_is_retried()
    {
        var transport = new SequenceHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        using var client = CreateClient(
            transport,
            () => "old-token",
            _ => Task.FromResult<string?>("new-token"),
            () => { });
        using var content = new StringContent("{\"quantity\":2}");

        using var response = await client.PostAsync(
            "https://cartex.test/sales", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["{\"quantity\":2}", "{\"quantity\":2}"], transport.Requests.Select(request => request.Body));
    }

    [Fact]
    public async Task WP11_server_date_changes_expiration_decision()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        Assert.False(ServerClock.IsExpiringSoon(expiresAt));
        var transport = new SequenceHandler(HttpStatusCode.OK)
        {
            Date = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        using var client = CreateClient(transport, () => null, null, () => { });

        try
        {
            using var response = await client.GetAsync(
                "https://cartex.test/clock", TestContext.Current.CancellationToken);
            Assert.True(ServerClock.IsExpiringSoon(expiresAt));
            Assert.InRange(ServerClock.Offset, TimeSpan.FromMinutes(29.9), TimeSpan.FromMinutes(30.1));
        }
        finally
        {
            ServerClock.Update(DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public async Task WP11_force_refresh_is_single_flight()
    {
        var api = new FakeAuthApi();
        var auth = new AuthService(api, new MemoryTokenStore());
        await auth.LoginAsync("user", "password", true);

        var tokens = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => auth.ForceRefreshAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(1, api.RefreshCalls);
        Assert.All(tokens, token => Assert.Equal("new-token", token));
    }

    private static HttpClient CreateClient(
        HttpMessageHandler transport,
        Func<string?> tokenProvider,
        Func<CancellationToken, Task<string?>>? forceRefreshAsync,
        Action onUnauthorized)
    {
        var handler = new AuthTokenHandler(
            tokenProvider,
            forceRefreshAsync: forceRefreshAsync,
            onUnauthorized: onUnauthorized)
        {
            InnerHandler = transport
        };
        return new HttpClient(handler);
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int _index;

        public DateTimeOffset? Date { get; init; }
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RequestSnapshot(request.Headers.Authorization?.Parameter, body));
            var response = new HttpResponseMessage(statuses[_index++]);
            response.Headers.Date = Date;
            return response;
        }
    }

    private sealed record RequestSnapshot(string? Token, string? Body);

    private sealed class FakeAuthApi : IAuthApi
    {
        public int RefreshCalls { get; private set; }

        public Task<LoginResponse> LoginAsync(LoginRequest request) =>
            Task.FromResult(new LoginResponse("old-token", "refresh-token", "User", "Seller"));

        public async Task<LoginResponse> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            await Task.Delay(50, cancellationToken);
            return new LoginResponse("new-token", "new-refresh-token", "User", "Seller");
        }

        public Task<LoginResponse> LoginWithKeyAsync(LoginWithKeyRequest request) => throw new NotSupportedException();
        public Task<LoginMethodsDto> GetLoginMethodsAsync() => throw new NotSupportedException();
        public Task<QrLoginStartResponse> StartQrAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IApiResponse<LoginResponse>> PollQrAsync(PollQrLoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ChangePasswordAsync(ChangePasswordRequest request) => throw new NotSupportedException();
    }

    private sealed class MemoryTokenStore : ITokenStore
    {
        private TokenBundle? _bundle;

        public void Save(TokenBundle bundle) => _bundle = bundle;
        public TokenBundle? Load() => _bundle;
        public void Clear() => _bundle = null;
    }
}
