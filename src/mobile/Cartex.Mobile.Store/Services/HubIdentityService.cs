using Cartex.Hub;

namespace Cartex.Mobile.Store.Services;

// HUB-04: qurilma kaliti bir marta yaratiladi va `SecureStorage` (Android Keystore) da qoladi.
// Kalit ham HUB tinglovchisiga (server sertifikati), ham yo'ldosh kanaliga (klient sertifikati)
// kerak, shuning uchun u bitta joydan beriladi.
public sealed class HubIdentityService : IHubIdentityStore, IDisposable
{
    private const string MaterialKey = "hub_identity";

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

    public void Dispose()
    {
        _channel?.Dispose();
        _identity?.Dispose();
        _lock.Dispose();
    }

    // Qurilma almashsa yoki chiqib ketilsa kalit qoladi: u do'konga emas, qurilmaga tegishli va
    // keyingi guvohnoma o'sha kalitga qayta bog'lanadi.
    public Task<string?> ReadAsync(CancellationToken cancellationToken) => SecureStorage.GetAsync(MaterialKey);

    public Task WriteAsync(string material, CancellationToken cancellationToken) =>
        SecureStorage.SetAsync(MaterialKey, material);
}
