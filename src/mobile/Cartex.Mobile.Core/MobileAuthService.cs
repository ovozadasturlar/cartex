using System.IdentityModel.Tokens.Jwt;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;

namespace Cartex.Mobile.Core;

public sealed class MobileAuthService(IAuthApi authApi, SessionStore session)
{
    private sealed record TokenClaims(string Token, long? UserId, long? BranchId, DateTime ValidTo);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private TokenClaims? _claims;

    public string DeviceName => MobileDeviceIdentity.DeviceName;
    public string DeviceId => MobileDeviceIdentity.DeviceId;

    public string FullName => Preferences.Get("user_fullname", "");
    public string Role => Preferences.Get("user_role", "");

    public long? UserId => Claims()?.UserId;
    public long? DefaultBranchId => Claims()?.BranchId;

    private TokenClaims? Claims()
    {
        var token = session.AccessToken;
        return token is null ? null : ClaimsFor(token);
    }

    private TokenClaims ClaimsFor(string token)
    {
        var cached = _claims;
        if (cached is not null && cached.Token == token) return cached;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            cached = new TokenClaims(token, ClaimId(jwt, "userId"), ClaimId(jwt, "defaultBranchId"), jwt.ValidTo);
        }
        catch
        {
            cached = new TokenClaims(token, null, null, DateTime.MinValue);
        }
        _claims = cached;
        return cached;
    }

    private static long? ClaimId(JwtSecurityToken jwt, string type)
    {
        var value = jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }

    public async Task LoginAsync(string username, string password)
    {
        var response = await authApi.LoginAsync(new LoginRequest(username, password, DeviceName, DeviceId));
        Preferences.Set("user_fullname", response.FullName);
        Preferences.Set("user_role", response.Role);
        await session.SaveAsync(response.Token, response.RefreshToken);
    }

    public async Task ChangePasswordAsync(string current, string next) =>
        await authApi.ChangePasswordAsync(new ChangePasswordRequest(current, next));

    public async Task<bool> TryRestoreAsync()
    {
        await session.LoadAsync();
        return session.AccessToken is not null && !string.IsNullOrEmpty(session.RefreshToken);
    }

    public event Action? SessionInvalidated;

    public async Task ValidateSessionAsync()
    {
        var refresh = session.RefreshToken;
        if (string.IsNullOrEmpty(refresh)) return;
        await _refreshLock.WaitAsync();
        try
        {
            var response = await authApi.RefreshAsync(new RefreshRequest(refresh, DeviceName, DeviceId));
            await session.SaveAsync(response.Token, response.RefreshToken);
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 401)
        {
            session.Clear();
            SessionInvalidated?.Invoke();
        }
        catch
        {
        }
        finally
        {
            _refreshLock.Release();
        }
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
            var response = await authApi.RefreshAsync(new RefreshRequest(refresh, DeviceName, DeviceId));
            await session.SaveAsync(response.Token, response.RefreshToken);
            return response.Token;
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 401)
        {
            session.Clear();
            SessionInvalidated?.Invoke();
            return null;
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

    public Task LogoutAsync()
    {
        var refresh = session.RefreshToken;
        session.Clear();
        if (!string.IsNullOrEmpty(refresh))
            _ = RevokeAsync(refresh);
        return Task.CompletedTask;
    }

    private async Task RevokeAsync(string refresh)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await authApi.LogoutAsync(new LogoutRequest(refresh), cancellation.Token); }
        catch { }
    }

    private bool IsExpiringSoon(string token) =>
        ClaimsFor(token).ValidTo <= DateTime.UtcNow.AddSeconds(60);
}
