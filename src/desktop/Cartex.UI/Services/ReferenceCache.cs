using System.Collections.Concurrent;
using Cartex.ApiClient;

namespace Cartex.UI.Services;

public sealed class ReferenceCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (DateTime At, Task Task)> _map = new();

    public Task<T> GetAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_map.TryGetValue(key, out var entry)
            && DateTime.UtcNow - entry.At < Ttl
            && entry.Task is Task<T> cached
            && !cached.IsFaulted && !cached.IsCanceled)
            return cached;

        var task = FetchAsync(key, factory);
        _map[key] = (DateTime.UtcNow, task);
        return task;
    }

    private async Task<T> FetchAsync<T>(string key, Func<Task<T>> factory)
    {
        using var detached = PageRequestScope.Detach();
        try { return await factory(); }
        catch
        {
            _map.TryRemove(key, out _);
            throw;
        }
    }

    public void Invalidate(params string[] keys)
    {
        foreach (var key in keys)
            _map.TryRemove(key, out _);
    }

    public void Clear() => _map.Clear();
}

public static class CacheKeys
{
    public const string Categories = "categories";
    public const string Units = "units";
    public const string ProductTypes = "product-types";
    public const string Manufacturers = "manufacturers";
    public const string Warehouses = "warehouses";
    public const string Business = "business";
    public const string Rates = "rates";
    public const string SalesPolicy = "sales-policy";
    public const string Receipt = "receipt";
    public const string Features = "features";
    public const string ProductLookup = "product-lookup";
}
