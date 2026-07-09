namespace Cartex.UI.Services;

public static class ImageUrl
{
    public static string? Absolute(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/')
            ? SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + url
            : url;
}
