namespace Cartex.Mobile.Core;

public sealed class SessionStore
{
    private string? _access;
    private string? _refresh;

    public string ServerUrl
    {
        get => Preferences.Get("server_url", "http://10.0.2.2:5015");
        set => Preferences.Set("server_url", value.TrimEnd('/'));
    }

    public string? AccessToken => _access;
    public string? RefreshToken => _refresh;

    public async Task LoadAsync()
    {
        _access = await SecureStorage.GetAsync("access_token");
        _refresh = await SecureStorage.GetAsync("refresh_token");
    }

    public async Task SaveAsync(string access, string refresh)
    {
        _access = access;
        _refresh = refresh;
        await SecureStorage.SetAsync("access_token", access);
        await SecureStorage.SetAsync("refresh_token", refresh);
    }

    public void Clear()
    {
        _access = null;
        _refresh = null;
        SecureStorage.Remove("access_token");
        SecureStorage.Remove("refresh_token");
    }
}
