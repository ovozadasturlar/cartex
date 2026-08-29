using System.Collections.Concurrent;
using Cartex.Application.Common.Interfaces;

namespace Cartex.Api.Hubs;

public sealed class HubPresence : IHubPresence
{
    private readonly ConcurrentDictionary<string, byte> _connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _channels = new(StringComparer.Ordinal);

    public bool IsOnline(string channel) =>
        !string.IsNullOrWhiteSpace(channel) && _channels.TryGetValue(channel, out var count) && count > 0;

    public void Join(string connectionId, string channel)
    {
        if (!_connections.TryAdd(Key(connectionId, channel), 0)) return;
        _channels.AddOrUpdate(channel, 1, (_, count) => count + 1);
    }

    public void Leave(string connectionId, string channel)
    {
        if (!_connections.TryRemove(Key(connectionId, channel), out _)) return;
        if (_channels.AddOrUpdate(channel, 0, (_, count) => count - 1) <= 0)
            _channels.TryRemove(new KeyValuePair<string, int>(channel, 0));
    }

    public IReadOnlyList<string> LeaveAll(string connectionId)
    {
        var prefix = connectionId + "|";
        var left = new List<string>();
        foreach (var key in _connections.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var channel = key[prefix.Length..];
            Leave(connectionId, channel);
            left.Add(channel);
        }
        return left;
    }

    private static string Key(string connectionId, string channel) => connectionId + "|" + channel;
}
