using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cartex.Hub;

namespace Cartex.UI.Services;

/// HUB-04: qurilma kaliti bir marta yaratiladi va himoyalangan faylda qoladi. Kalit ham HUB
/// tinglovchisiga (server sertifikati), ham yo'ldosh kanaliga (klient sertifikati) kerak,
/// shuning uchun u bitta joydan beriladi.
public sealed class HubIdentityService : IHubIdentityStore, IDisposable
{
    private readonly ProtectedFileStore _store = new(
        "hub-identity.bin", "hub-identity.key", "Cartex.HubIdentity.v1");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private HubIdentityKey? _identity;
    private HttpClient? _channel;

    public async Task<HubIdentityKey> KeyAsync(CancellationToken cancellationToken = default)
    {
        if (_identity is not null) return _identity;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _identity ??= await HubIdentityKey.LoadAsync(this, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<HttpClient> ChannelAsync(CancellationToken cancellationToken = default)
    {
        var identity = await KeyAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _channel ??= HubTransport.Create(identity, TimeSpan.FromSeconds(15));
        }
        finally
        {
            _lock.Release();
        }
    }

    // Sertifikat Windowsda vaqtinchalik kalit konteyneri ochadi — dastur yopilganda u qaytariladi.
    public void Dispose()
    {
        _channel?.Dispose();
        _identity?.Dispose();
        _lock.Dispose();
    }

    Task<string?> IHubIdentityStore.ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_store.Read() is { } plain ? Encoding.UTF8.GetString(plain) : null);

    Task IHubIdentityStore.WriteAsync(string material, CancellationToken cancellationToken)
    {
        _store.Write(Encoding.UTF8.GetBytes(material));
        return Task.CompletedTask;
    }
}
