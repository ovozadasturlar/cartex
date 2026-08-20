using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cartex.Shared.Models.OfflineCache;

/// HUB-04: guvohnoma bulut kaliti bilan imzolanadi va do'kon tarmog'ida **oflayn** tekshiriladi.
/// Shuning uchun imzo assimetrik: ochiq kalit hamma qurilmada bo'lishi mumkin, yopig'i faqat serverda.
public static class HubAttestation
{
    public static string Sign(ECDsa key, HubAttestationPayload payload)
    {
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(payload, HubJson.Options));
        var signature = key.SignData(Encoding.UTF8.GetBytes(body), HashAlgorithmName.SHA256);
        return body + "." + Encode(signature);
    }

    public static HubAttestationPayload? Verify(string? token, string? publicKey, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(publicKey))
            return null;

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
            return null;

        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            var body = token[..dot];
            if (!key.VerifyData(Encoding.UTF8.GetBytes(body), Decode(token[(dot + 1)..]), HashAlgorithmName.SHA256))
                return null;

            var payload = JsonSerializer.Deserialize<HubAttestationPayload>(Decode(body), HubJson.Options);
            return payload is not null && payload.ExpiresAt > utcNow ? payload : null;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    /// Guvohnomaga yozilishidan oldin qurilma kaliti shakl jihatidan tekshiriladi — noto'g'ri
    /// qiymat yozilsa, o'sha qurilma keyin hech qachon ulana olmasdi.
    public static bool IsPublicKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(value), out _);
            return key.KeySize == 256;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }

    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '='));
    }
}
