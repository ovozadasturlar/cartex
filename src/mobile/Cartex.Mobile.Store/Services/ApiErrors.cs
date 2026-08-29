using System.Text.Json;
using Cartex.Mobile.Core;
using Refit;

namespace Cartex.Mobile.Store.Services;

public static class ApiErrors
{
    public static string Describe(ApiException ex)
    {
        try
        {
            using var doc = JsonDocument.Parse(ex.Content ?? "");
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d) return d;
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t)
                return t == "feature_locked" ? Loc.Instance["feature_locked"] : t;
        }
        catch { }
        return (int)ex.StatusCode switch
        {
            401 => Loc.Instance["err_session_expired"],
            403 => Loc.Instance["err_forbidden"],
            _ => string.Format(Loc.Instance["err_server_fmt"], (int)ex.StatusCode)
        };
    }
}
