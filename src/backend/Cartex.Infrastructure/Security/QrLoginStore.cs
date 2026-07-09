using System.Collections.Concurrent;
using System.Security.Cryptography;
using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common.Exceptions;

namespace Cartex.Infrastructure.Security;

public sealed class QrLoginStore : IQrLoginStore
{
    private sealed class Entry
    {
        public DateTime ExpiresAt { get; init; }
        public long? UserId { get; set; }
    }

    private const int MaxPending = 200;
    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public string Start(TimeSpan ttl)
    {
        Cleanup();
        if (_sessions.Count >= MaxPending)
            throw new BusinessRuleException("Keyinroq urinib ko'ring.");
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _sessions[code] = new Entry { ExpiresAt = DateTime.UtcNow + ttl };
        return code;
    }

    public bool Approve(string code, long userId)
    {
        Cleanup();
        if (!_sessions.TryGetValue(code, out var entry) || entry.ExpiresAt < DateTime.UtcNow)
            return false;
        entry.UserId = userId;
        return true;
    }

    public long? TakeApproved(string code)
    {
        if (!_sessions.TryGetValue(code, out var entry)) return null;
        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _sessions.TryRemove(code, out _);
            return null;
        }
        if (entry.UserId is null) return null;
        _sessions.TryRemove(code, out _);
        return entry.UserId;
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, entry) in _sessions)
            if (entry.ExpiresAt < now)
                _sessions.TryRemove(key, out _);
    }
}
