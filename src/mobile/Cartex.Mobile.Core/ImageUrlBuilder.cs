namespace Cartex.Mobile.Core;

public sealed class ImageUrlBuilder(SessionStore session)
{
    public string? Full(string? relativeOrAbsolute, bool thumb = false)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return null;

        var url = relativeOrAbsolute.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = session.ServerUrl.TrimEnd('/') + (url.StartsWith('/') ? url : "/" + url);

        if (!thumb || url.Contains("thumb=true", StringComparison.OrdinalIgnoreCase))
            return url;

        return url + (url.Contains('?') ? "&" : "?") + "thumb=true";
    }

    public string? FromKey(string? key, bool thumb = false) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Full($"/api/storage/content?key={Uri.EscapeDataString(key)}", thumb);
}
