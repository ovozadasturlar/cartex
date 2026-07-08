using System.IdentityModel.Tokens.Jwt;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;

namespace Cartex.Mobile.Agent.Services;

public sealed class MobileAuthService(IAuthApi authApi, SessionStore session)
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public string DeviceName { get; } = DeviceInfo.Current.Name is { Length: > 0 } name ? name : DeviceInfo.Current.Model;

    public async Task LoginAsync(string username, string password)
    {
        var response = await authApi.LoginAsync(new LoginRequest(username, password, DeviceName));
        await session.SaveAsync(response.Token, response.RefreshToken);
    }

    public async Task<bool> TryRestoreAsync()
    {
        await session.LoadAsync();
        return session.AccessToken is not null && !string.IsNullOrEmpty(session.RefreshToken);
    }

    public async Task<string?> EnsureFreshTokenAsync(CancellationToken cancellationToken)
    {
        var token = session.AccessToken;
        if (token is null) return null;
        if (!IsExpiringSoon(token)) return token;
        if (string.IsNullOrEmpty(session.RefreshToken)) return token;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            token = session.AccessToken;
            if (token is not null && !IsExpiringSoon(token)) return token;
            var refresh = session.RefreshToken;
            if (string.IsNullOrEmpty(refresh)) return token;
            var response = await authApi.RefreshAsync(new RefreshRequest(refresh, DeviceName));
            await session.SaveAsync(response.Token, response.RefreshToken);
            return response.Token;
        }
        catch
        {
            return session.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        var refresh = session.RefreshToken;
        session.Clear();
        if (!string.IsNullOrEmpty(refresh))
            try { await authApi.LogoutAsync(new LogoutRequest(refresh)); } catch { }
    }

    private static bool IsExpiringSoon(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo <= DateTime.UtcNow.AddSeconds(60);
        }
        catch
        {
            return true;
        }
    }
}
