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

    public IReadOnlySet<string> Snapshot() => new HashSet<string>(Current(), StringComparer.Ordinal);

    private HashSet<string> Current()
    {
        var token = session.AccessToken;
        if (token == _token) return _permissions;
        _token = token;
        _permissions = token is null ? [] : JwtClaims.Parse(token)?.All("permission").ToHashSet() ?? [];
        return _permissions;
    }
}
