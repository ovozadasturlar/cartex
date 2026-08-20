using System.Text.Json;

namespace Cartex.UI.Services;

public sealed record OfflineLeaseCredential(
    string DeviceId,
    long LeaseId,
    long WarehouseId,
    long Epoch,
    string Token,
    long LastAcceptedSequence);

/// Bearer'ga o'xshash oflayn vakolat tokeni settings.json dan tashqarida, himoyalangan faylda turadi.
public sealed class OfflineLeaseCredentialStore
{
    private readonly ProtectedFileStore _store = new(
        "offline-authority.bin", "offline-authority.key", "Cartex.OfflineAuthority.v2");

    public OfflineLeaseCredential? Load()
    {
        if (_store.Read() is not { } plain) return null;
        try
        {
            return JsonSerializer.Deserialize<OfflineLeaseCredential>(plain);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(OfflineLeaseCredential credential) =>
        _store.Write(JsonSerializer.SerializeToUtf8Bytes(credential));

    public void Clear() => _store.Clear();
}
