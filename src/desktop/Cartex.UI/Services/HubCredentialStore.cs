using System.Text.Json;

namespace Cartex.UI.Services;

/// HUB-04/HUB-05: `Token` — bulut imzolagan guvohnoma, `ServerKey` uni oflaynda tekshirish uchun
/// serverning ochiq kaliti, `AuthorityEpoch` bulutdan o'qilgan oxirgi vakolat `epoch`i (eski
/// HUB'ni rad etish chegarasi), `Endpoint` esa oxirgi muvaffaqiyatli ulanish manzili (`HUB-11`).
public sealed record HubCredential(string Token, string ServerKey, long AuthorityEpoch, string? Endpoint);

/// Guvohnoma settings.json da ochiq matnda turmaydi: uni o'qigan tomon kalitni isbotlay olmagani
/// uchun HUB'ga kira olmaydi (`HUB-04`), lekin token do'kon, ombor, qurilma va foydalanuvchi
/// belgilarini ochiq olib yuradi — u lease tokeni bilan bir xil himoyada saqlanadi.
public sealed class HubCredentialStore
{
    private readonly ProtectedFileStore _store = new(
        "hub-attestation.bin", "hub-attestation.key", "Cartex.HubAttestation.v1");

    public HubCredential? Load()
    {
        if (_store.Read() is not { } plain) return null;
        try
        {
            return JsonSerializer.Deserialize<HubCredential>(plain);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(HubCredential credential) =>
        _store.Write(JsonSerializer.SerializeToUtf8Bytes(credential));
}
