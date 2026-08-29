using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;
using Cartex.UI.Services;
using Xunit;

namespace Cartex.UnitTests;

public sealed class BootstrapAuthenticationGateTests
{
    [Fact]
    public async Task WP20_hub_attestation_does_not_start_without_authentication()
    {
        var auth = new AuthService(null!, new MemoryTokenStore());
        var hub = new HubClientService(null!, null!, null!, auth);

        await hub.RefreshAttestationAsync();
    }

    [Fact]
    public async Task WP20_network_failure_during_refresh_keeps_session()
    {
        var store = new MemoryTokenStore();
        var auth = new AuthService(new NetworkFailingAuthApi(), store);
        await auth.LoginAsync("user", "password", true);

        var token = await auth.ForceRefreshAsync(TestContext.Current.CancellationToken);

        Assert.Equal("access-token", token);
        Assert.True(auth.IsAuthenticated);
        Assert.NotNull(store.Load());
    }

    private sealed class MemoryTokenStore : ITokenStore
    {
        private TokenBundle? _bundle;

        public void Save(TokenBundle bundle) => _bundle = bundle;
        public TokenBundle? Load() => _bundle;
        public void Clear() => _bundle = null;
    }

    private sealed class NetworkFailingAuthApi : IAuthApi
    {
        public Task<LoginResponse> LoginAsync(LoginRequest request) =>
            Task.FromResult(new LoginResponse("access-token", "refresh-token", "User", "Seller"));

        public Task<LoginResponse> RefreshAsync(
            RefreshRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<LoginResponse>(new HttpRequestException("offline"));

        public Task<LoginResponse> LoginWithKeyAsync(LoginWithKeyRequest request) => throw new NotSupportedException();
        public Task<LoginMethodsDto> GetLoginMethodsAsync() => throw new NotSupportedException();
        public Task<QrLoginStartResponse> StartQrAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Refit.IApiResponse<LoginResponse>> PollQrAsync(PollQrLoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ChangePasswordAsync(ChangePasswordRequest request) => throw new NotSupportedException();
    }
}
