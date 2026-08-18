namespace Cartex.Mobile.Core;

public sealed class SessionStore
{
    private const string HasSessionKey = "has_session";

    private string? _access;
    private string? _refresh;
    private Task? _loading;

    // Tokenlar Android keystore'dan o'qiladi va bu ~1 sekund oladi. Ilova qaysi ekrandan
    // boshlashini shu sirsiz bayroq hal qiladi, shunda keystore kutilmaydi.
    public static bool HasSession => Preferences.Get(HasSessionKey, Preferences.ContainsKey("user_fullname"));

    public string ServerUrl
    {
        get => Preferences.Get("server_url", "http://10.0.2.2:5015");
        set => Preferences.Set("server_url", value.TrimEnd('/'));
    }

    public string? AccessToken => _access;
    public string? RefreshToken => _refresh;

    public Task LoadAsync() => _loading ??= Task.Run(ReadAsync);

    private async Task ReadAsync()
    {
        if (!HasSession) return;
        try
        {
            _access = await SecureStorage.GetAsync("access_token").ConfigureAwait(false);
            _refresh = await SecureStorage.GetAsync("refresh_token").ConfigureAwait(false);
        }
        catch
        {
            _access = _refresh = null;
        }
    }

    public async Task SaveAsync(string access, string refresh)
    {
        _access = access;
        _refresh = refresh;
        _loading = Task.CompletedTask;
        Preferences.Set(HasSessionKey, true);
        await SecureStorage.SetAsync("access_token", access);
        await SecureStorage.SetAsync("refresh_token", refresh);
    }

    public void Clear()
    {
        _access = null;
        _refresh = null;
        _loading = Task.CompletedTask;
        Preferences.Set(HasSessionKey, false);
        Preferences.Remove("user_fullname");
        Preferences.Remove("user_role");
        SecureStorage.Remove("access_token");
        SecureStorage.Remove("refresh_token");
    }
}
