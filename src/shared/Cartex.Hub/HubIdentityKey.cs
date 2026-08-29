using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;

namespace Cartex.Hub;

/// Qurilma kalitini qayerda saqlashni klient hal qiladi: kompyuterda himoyalangan fayl,
/// telefonda `SecureStorage`. Shu yerda faqat matn beriladi va olinadi.
public interface IHubIdentityStore
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(string material, CancellationToken cancellationToken);
}

/// HUB-04: guvohnoma bearer emas. Qurilma o'z ECDSA P-256 juftligini yaratadi, ochiq kalitini
/// guvohnomaga yozdiradi va TLS qo'l siqishida shu kalitga **egaligini** isbotlaydi. Shuning uchun
/// tarmoqda ushlangan guvohnoma yolg'iz o'zi hech qayerga kirgizmaydi.
public sealed class HubIdentityKey : IDisposable
{
    private const string Subject = "CN=cartex-hub";
    private static readonly TimeSpan CertificateLifetime = TimeSpan.FromDays(400);

    private readonly ECDsa _key;

    private HubIdentityKey(ECDsa key, X509Certificate2 certificate)
    {
        _key = key;
        Certificate = certificate;
        PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    /// SPKI, base64 — guvohnomadagi `pk` ayni shu qiymat.
    public string PublicKey { get; }

    public X509Certificate2 Certificate { get; }

    public static async Task<HubIdentityKey> LoadAsync(
        IHubIdentityStore store, CancellationToken cancellationToken = default)
    {
        var stored = Material(await store.ReadAsync(cancellationToken));
        var key = Import(stored?.Key);
        var restored = key is null ? null : Restore(stored?.Certificate);
        key ??= ECDsa.Create(ECCurve.NamedCurves.nistP256);

        using var certificate = restored ?? Issue(key);
        if (restored is null)
            await store.WriteAsync(
                JsonSerializer.Serialize(
                    new HubIdentityMaterial(
                        Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
                        Convert.ToBase64String(certificate.RawData)),
                    HubJson.Options),
                cancellationToken);

        return new HubIdentityKey(key, Bind(certificate, key));
    }

    public static string? PublicKeyOf(X509Certificate2? certificate)
    {
        try
        {
            return certificate is null
                ? null
                : Convert.ToBase64String(certificate.PublicKey.ExportSubjectPublicKeyInfo());
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static bool Matches(X509Certificate2? certificate, string? publicKey) =>
        SameKey(PublicKeyOf(certificate), publicKey);

    public static bool SameKey(string? publicKey, string? expected) =>
        !string.IsNullOrEmpty(expected) && string.Equals(publicKey, expected, StringComparison.Ordinal);

    public void Dispose()
    {
        Certificate.Dispose();
        _key.Dispose();
    }

    /// Windows SChannel efemer kalitli sertifikatni server sifatida qabul qilmaydi, shuning uchun
    /// kalit PKCS#12 orqali qayta yuklanadi — shundagina TLS qo'l siqishi kalitni ko'ra oladi.
    private static X509Certificate2 Bind(X509Certificate2 certificate, ECDsa key)
    {
        using var bound = certificate.HasPrivateKey ? null : certificate.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(
            (bound ?? certificate).Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 Issue(ECDsa key)
    {
        var now = DateTimeOffset.UtcNow;
        return new CertificateRequest(Subject, key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(now.AddDays(-1), now + CertificateLifetime);
    }

    private static X509Certificate2? Restore(string? certificate)
    {
        if (string.IsNullOrWhiteSpace(certificate)) return null;
        try
        {
            var loaded = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate));
            if (loaded.NotAfter > DateTime.Now.AddDays(1)) return loaded;
            loaded.Dispose();
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
        }
        return null;
    }

    private static ECDsa? Import(string? privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey)) return null;
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
            return key;
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            key.Dispose();
            return null;
        }
    }

    private static HubIdentityMaterial? Material(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        try
        {
            return JsonSerializer.Deserialize<HubIdentityMaterial>(stored, HubJson.Options)
                is { Key.Length: > 0 } material
                ? material
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed record HubIdentityMaterial(string Key, string Certificate);
