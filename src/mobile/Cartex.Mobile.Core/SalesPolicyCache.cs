using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;

namespace Cartex.Mobile.Core;

public sealed class SalesPolicyCache(ISettingsApi settingsApi)
{
    private const string CacheKey = "sales_policy";
    private SalesPolicyDto? _current;
    private Task? _inFlight;
    private bool _loaded;
    private int _generation;

    public SalesPolicyDto Current => _current is { } current && Preferences.ContainsKey(CacheKey)
        ? current
        : _current = Read();

    public bool LastRefreshSucceeded { get; private set; }
    public Exception? LastRefreshError { get; private set; }
    public bool HasCachedData => Preferences.ContainsKey(CacheKey);

    public Task EnsureLoadedAsync() =>
        _loaded && Preferences.ContainsKey(CacheKey)
            ? Task.CompletedTask
            : _inFlight is { IsCompleted: false } inFlight
                ? inFlight
                : _inFlight = RefreshAsync(Volatile.Read(ref _generation));

    public Task<SalesPolicyDto> RefreshAsync() => RefreshAsync(Volatile.Read(ref _generation));

    private async Task<SalesPolicyDto> RefreshAsync(int generation)
    {
        LastRefreshSucceeded = false;
        LastRefreshError = null;
        try
        {
            var current = await settingsApi.GetSalesPolicyAsync();
            if (generation != Volatile.Read(ref _generation)) return Current;
            _current = current;
            _loaded = true;
            LastRefreshSucceeded = true;
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch (Exception exception)
        {
            if (generation != Volatile.Read(ref _generation)) return Current;
            LastRefreshError = exception;
            _current = Read();
        }
        return _current;
    }

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

    private static SalesPolicyDto Read()
    {
        try
        {
            var json = Preferences.Get(CacheKey, "");
            return string.IsNullOrWhiteSpace(json)
                ? new SalesPolicyDto()
                : JsonSerializer.Deserialize<SalesPolicyDto>(json) ?? new SalesPolicyDto();
        }
        catch
        {
            return new SalesPolicyDto();
        }
    }
}
