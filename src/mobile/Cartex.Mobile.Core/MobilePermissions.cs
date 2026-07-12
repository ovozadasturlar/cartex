using System.IdentityModel.Tokens.Jwt;

namespace Cartex.Mobile.Core;

public sealed class MobilePermissions(SessionStore session)
{
    private string? _token;
    private HashSet<string> _permissions = [];

    public bool Has(string permission)
    {
        var set = Current();
        return set.Contains("*") || set.Contains(permission);
    }

    public bool HasAny(params string[] permissions) => permissions.Any(Has);

    private HashSet<string> Current()
    {
        var token = session.AccessToken;
        if (token == _token) return _permissions;
        _token = token;
        try
        {
            _permissions = token is null
                ? []
                : new JwtSecurityTokenHandler().ReadJwtToken(token)
                    .Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();
        }
        catch
        {
            _permissions = [];
        }
        return _permissions;
    }
}
