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

        var jwt = JwtClaims.Parse(token);
        cached = jwt is null
            ? new TokenClaims(token, null, null, DateTime.MinValue)
            : new TokenClaims(token, jwt.Number("userId"), jwt.Number("defaultBranchId"), jwt.ValidTo);
        _claims = cached;
        return cached;
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

    // Refresh tokeni har yangilashda aylanadi, shuning uchun tekshirish ham aynan shu qulf
    // ostidagi yo'ldan o'tadi: eskirgan token bilan ikkinchi urinish 401 qaytarib
    // foydalanuvchini bekordan-bekorga tizimdan chiqarib yuborardi.
    public Task ValidateSessionAsync() => EnsureFreshTokenAsync(CancellationToken.None);

    public async Task<string?> EnsureFreshTokenAsync(CancellationToken cancellationToken)
    {
        await session.LoadAsync();
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

    // Bekor qilish kutiladi: aks holda so'rov hali yo'ldayligida chaqiruvchi server
    // manzilini almashtirsa, refresh token yangi (ishonchsiz) hostga ketishi mumkin.
    public Task LogoutAsync()
    {
        var refresh = session.RefreshToken;
        session.Clear();
        return string.IsNullOrEmpty(refresh) ? Task.CompletedTask : RevokeAsync(refresh);
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
