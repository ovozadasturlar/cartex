using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;

namespace Cartex.UI.Services;

public sealed class AuthService
{
    private readonly IAuthApi _authApi;
    private readonly ITokenStore _tokenStore;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _token;
    private string? _refreshToken;
    private bool _persist;

    public string DeviceName { get; } = Environment.MachineName;
    public string DeviceId => SettingsService.Instance.DeviceId;

    public AuthService(IAuthApi authApi, ITokenStore tokenStore)
    {
        _authApi = authApi;
        _tokenStore = tokenStore;
    }

    public string? Token
    {
        get => _token;
        private set
        {
            _token = value;
            UserInfo = value is not null ? ParseToken(value) : null;
            _permissionSet = UserInfo is null
                ? null
                : new HashSet<string>(UserInfo.Permissions, StringComparer.OrdinalIgnoreCase);
        }
    }

    public UserInfo? UserInfo { get; private set; }
    private HashSet<string>? _permissionSet;
    public bool IsAuthenticated => Token is not null && UserInfo is not null;

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe)
    {
        var response = await _authApi.LoginAsync(new LoginRequest(username, password, DeviceName, DeviceId));
        Apply(response, rememberMe);
        return response;
    }

    public async Task<LoginResponse> LoginWithKeyAsync(string keyContent, string serial)
    {
        var response = await _authApi.LoginWithKeyAsync(new LoginWithKeyRequest(keyContent, serial, $"{DeviceName} · USB kalit", DeviceId));
        Apply(response, true);
        return response;
    }

    public async Task<LoginMethodsDto> GetLoginMethodsAsync()
    {
        try { return await _authApi.GetLoginMethodsAsync(); }
        catch { return new LoginMethodsDto(false, true); }
    }

    public Task<QrLoginStartResponse> StartQrAsync(CancellationToken cancellationToken) => _authApi.StartQrAsync(cancellationToken);

    public async Task<LoginResponse?> TryQrPollAsync(string code, CancellationToken cancellationToken)
    {
        var response = await _authApi.PollQrAsync(new PollQrLoginRequest(code, $"{DeviceName} · QR", DeviceId), cancellationToken);
        if (response.Content is null) return null;
        Apply(response.Content, true);
        return response.Content;
    }

    private void Apply(LoginResponse response, bool persist)
    {
        Token = response.Token;
        _refreshToken = response.RefreshToken;
        _persist = persist;
        if (persist)
            _tokenStore.Save(new TokenBundle(response.Token, response.RefreshToken));
        else
            _tokenStore.Clear();
    }

    public bool TryRestore()
    {
        var bundle = _tokenStore.Load();
        if (bundle is null) return false;
        if (string.IsNullOrEmpty(bundle.Refresh) && IsExpiringSoon(bundle.Access))
        {
            _tokenStore.Clear();
            return false;
        }
        Token = bundle.Access;
        _refreshToken = bundle.Refresh;
        _persist = true;
        return UserInfo is not null;
    }

    public async Task<string?> EnsureFreshTokenAsync(CancellationToken cancellationToken)
    {
        var token = _token;
        if (token is null) return null;
        if (!IsExpiringSoon(token)) return token;
        if (string.IsNullOrEmpty(_refreshToken)) return token;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && !IsExpiringSoon(_token)) return _token;
            var refresh = _refreshToken;
            if (string.IsNullOrEmpty(refresh)) return _token;
            var response = await _authApi.RefreshAsync(new RefreshRequest(refresh, DeviceName, DeviceId));
            Apply(response, _persist);
            return _token;
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 401)
        {
            SessionInvalidated?.Invoke();
            return _token;
        }
        catch
        {
            return _token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public event Action? LoggedOut;
    public event Action? SessionInvalidated;

    public async Task ValidateSessionAsync()
    {
        var refresh = _refreshToken;
        if (string.IsNullOrEmpty(refresh)) return;
        await _refreshLock.WaitAsync();
        try
        {
            var response = await _authApi.RefreshAsync(new RefreshRequest(refresh, DeviceName, DeviceId));
            Apply(response, _persist);
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 401)
        {
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

    public void Logout()
    {
        var refresh = _refreshToken;
        if (!string.IsNullOrEmpty(refresh))
            _ = RevokeAsync(refresh);
        Token = null;
        _refreshToken = null;
        _persist = false;
        _tokenStore.Clear();
        LoggedOut?.Invoke();
    }

    private async Task RevokeAsync(string refresh)
    {
        try { await _authApi.LogoutAsync(new LogoutRequest(refresh)); } catch { }
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

    public bool HasPermission(string permission) =>
        _permissionSet is not null
        && (_permissionSet.Contains("*")
            || permission.Split('|', StringSplitOptions.RemoveEmptyEntries).Any(_permissionSet.Contains));

    public IReadOnlyList<string> Roles => UserInfo?.Roles ?? [];

    private static UserInfo ParseToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var userId = long.TryParse(jwt.Claims.FirstOrDefault(c => c.Type == "userId")?.Value, out var id) ? id : 0;
            var username = jwt.Claims.FirstOrDefault(c => c.Type == "username")?.Value ?? "";
            var fullName = jwt.Claims.FirstOrDefault(c => c.Type == "fullName")?.Value ?? username;
            var roles = jwt.Claims.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
            var startPage = jwt.Claims.FirstOrDefault(c => c.Type == "startPage")?.Value;
            var permissions = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

            return new UserInfo(userId, username, fullName, roles, startPage, permissions);
        }
        catch
        {
            return new UserInfo(0, "", "", [], null, []);
        }
    }
}
