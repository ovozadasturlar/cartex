using System.Diagnostics;
using Cartex.ApiClient;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;

namespace Cartex.Mobile.Core;

public sealed class MobileAuthService(IAuthApi authApi, SessionStore session)
{
    private sealed record TokenClaims(string Token, long? UserId, long? BranchId, DateTime ValidTo);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private TokenClaims? _claims;
    private long _lastRefreshTimestamp;

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

    public Task ValidateSessionAsync() => EnsureFreshTokenAsync(CancellationToken.None);

    public Task<string?> EnsureFreshTokenAsync(CancellationToken cancellationToken) =>
        RefreshAsync(false, cancellationToken);

    public Task<string?> ForceRefreshAsync(CancellationToken cancellationToken) =>
        RefreshAsync(true, cancellationToken);

    private async Task<string?> RefreshAsync(bool force, CancellationToken cancellationToken)
    {
        await session.LoadAsync();
        var token = session.AccessToken;
        if (token is null) return null;
        if (!force && !IsExpiringSoon(token)) return token;
        if (string.IsNullOrEmpty(session.RefreshToken)) return token;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            token = session.AccessToken;
            if (force && WasRefreshedRecently()) return token;
            if (!force && token is not null && !IsExpiringSoon(token)) return token;
            var refresh = session.RefreshToken;
            if (string.IsNullOrEmpty(refresh)) return token;
            var response = await authApi.RefreshAsync(
                new RefreshRequest(refresh, DeviceName, DeviceId), cancellationToken);
            _lastRefreshTimestamp = Stopwatch.GetTimestamp();
            await session.SaveAsync(response.Token, response.RefreshToken);
            return response.Token;
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 401)
        {
            session.Clear();
            SessionInvalidated?.Invoke();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Majburiy yangilash faqat 401 dan keyin, xuddi shu token bilan chaqiriladi. Shu yerda
        // tarmoq nosozligida eski tokenni qaytarsak, AuthTokenHandler "refreshed == token" deb
        // o'qib, vaqtinchalik uzilishni ham rad etilgan deb hisoblab operatorni smena o'rtasida
        // chiqarib yuborardi. Shuning uchun xato yuqoriga uzatiladi: so'rov oddiy tarmoq
        // xatosidek muvaffaqiyatsiz bo'ladi, sessiya esa saqlanib qoladi va keyingi so'rov
        // qayta urinadi. Haqiqiy rad javobi faqat yuqoridagi 401 filtridan o'tadi.
        catch when (force)
        {
            throw;
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

    private bool WasRefreshedRecently() =>
        _lastRefreshTimestamp != 0
        && Stopwatch.GetElapsedTime(_lastRefreshTimestamp) < TimeSpan.FromSeconds(5);

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
        ServerClock.IsExpiringSoon(ClaimsFor(token).ValidTo);
}
