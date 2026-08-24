using System.Text.Json;
using Cartex.ApiClient.Api;

namespace Cartex.Mobile.Core;

public sealed class MobileFeaturesCache(IFeaturesApi featuresApi)
{
    private const string CacheKey = "enabled_features";
    private List<string>? _current;
    private Task? _inFlight;
    private bool _loaded;
    private int _generation;

    public IReadOnlyList<string> Current => _current ??= Read();
    public bool LastRefreshSucceeded { get; private set; }
    public Exception? LastRefreshError { get; private set; }
    public bool HasCachedData => Preferences.ContainsKey(CacheKey);

    public Task EnsureLoadedAsync() =>
        _loaded && Preferences.ContainsKey(CacheKey) ? Task.CompletedTask : RefreshAsync();

    public Task RefreshAsync() =>
        _inFlight is { IsCompleted: false } inFlight
            ? inFlight
            : _inFlight = LoadAsync(Volatile.Read(ref _generation));

    public void Clear()
    {
        Interlocked.Increment(ref _generation);
        _current = null;
        _loaded = false;
        _inFlight = null;
        LastRefreshSucceeded = false;
        LastRefreshError = null;
        Preferences.Remove(CacheKey);
    }

    private async Task LoadAsync(int generation)
    {
        LastRefreshSucceeded = false;
        LastRefreshError = null;
        try
        {
            var current = await featuresApi.GetEnabledAsync();
            if (generation != Volatile.Read(ref _generation)) return;
            _current = current;
            _loaded = true;
            LastRefreshSucceeded = true;
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _generation)) return;
            LastRefreshError = exception;
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
