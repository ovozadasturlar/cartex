using System.Text.Json;
using Cartex.ApiClient.Api;

namespace Cartex.Mobile.Store.Services;

public sealed class MobileFeaturesCache(IFeaturesApi featuresApi)
{
    private const string CacheKey = "enabled_features";
    private List<string>? _current;
    private Task? _inFlight;
    private bool _loaded;

    public IReadOnlyList<string> Current => _current ??= Read();

    public bool QueueEnabled =>
        Current.Contains("ordering", StringComparer.OrdinalIgnoreCase)
        || Current.Contains("store", StringComparer.OrdinalIgnoreCase);

    // Ro'yxat bo'sh bo'lsa har bir modul o'chiq deb qaraladi, shuning uchun gating shu
    // kutilgandan keyin hisoblanadi. Saqlangan yozuv logout'da o'chiriladi — u yo'q bo'lsa
    // xotiradagi nusxa boshqa do'konniki, qaytadan yuklanadi.
    public Task EnsureLoadedAsync() =>
        _loaded && Preferences.ContainsKey(CacheKey) ? Task.CompletedTask : RefreshAsync();

    public Task RefreshAsync() =>
        _inFlight is { IsCompleted: false } inFlight ? inFlight : _inFlight = LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            _current = await featuresApi.GetEnabledAsync();
            _loaded = true;
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch
        {
            _current = Read();
        }
    }

    private static List<string> Read()
    {
        try
        {
            var json = Preferences.Get(CacheKey, "");
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
