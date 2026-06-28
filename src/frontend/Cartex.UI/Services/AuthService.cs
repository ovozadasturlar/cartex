using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Auth;

namespace Cartex.UI.Services;

public sealed class AuthService
{
    private readonly IAuthApi _authApi;
    private readonly ITokenStore _tokenStore;
    private string? _token;

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
            if (value is not null)
                UserInfo = ParseToken(value);
            else
                UserInfo = null;
        }
    }

    public UserInfo? UserInfo { get; private set; }
    public bool IsAuthenticated => Token is not null && UserInfo is not null;

    public async Task<LoginResponse> LoginAsync(string username, string password, bool rememberMe)
    {
        var response = await _authApi.LoginAsync(new LoginRequest(username, password));
        Token = response.Token;
        if (rememberMe)
            _tokenStore.Save(response.Token);
        else
            _tokenStore.Clear();
        return response;
    }

    public bool TryRestore()
    {
        var stored = _tokenStore.Load();
        if (stored is null) return false;
        if (IsExpired(stored))
        {
            _tokenStore.Clear();
            return false;
        }
        Token = stored;
        return UserInfo is not null;
    }

    public void Logout()
    {
        Token = null;
        _tokenStore.Clear();
    }

    private static bool IsExpired(string token)
    {
        try
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    public bool HasPermission(string permission) =>
        UserInfo?.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) == true;

    private static UserInfo ParseToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var userId = long.TryParse(jwt.Claims.FirstOrDefault(c => c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var username = jwt.Claims.FirstOrDefault(c => c.Type == "unique_name" || c.Type == ClaimTypes.Name)?.Value ?? "";
            var fullName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name" || c.Type == ClaimTypes.GivenName)?.Value ?? username;
            var role = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value ?? "";
            var permissions = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

            return new UserInfo(userId, username, fullName, role, permissions);
        }
        catch
        {
            return new UserInfo(0, "", "", "", []);
        }
    }
}
