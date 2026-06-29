using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;

namespace Cartex.Infrastructure.Security;

public sealed class HardwareKeyService(ISettingsService settings, ISecretProtector secrets) : IHardwareKeyService
{
    private sealed record KeyMaterial(string PublicKey, string PrivateKey);
    private sealed record Payload(string U, string S, long Iat);

    public async Task<string> IssueAsync(string username, string serial, CancellationToken cancellationToken = default)
    {
        var material = await GetOrCreateMaterialAsync(cancellationToken);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(username, serial, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(secrets.Unprotect(material.PrivateKey)), out _);
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{Convert.ToBase64String(payload)}.{Convert.ToBase64String(signature)}";
    }

    public async Task<string?> VerifyAsync(string keyContent, string serial, CancellationToken cancellationToken = default)
    {
        var material = await settings.GetAsync<KeyMaterial>(SettingKeys.HardwareKey, cancellationToken);
        if (material is null) return null;

        var parts = keyContent.Trim().Split('.');
        if (parts.Length != 2) return null;

        byte[] payloadBytes, signature;
        try
        {
            payloadBytes = Convert.FromBase64String(parts[0]);
            signature = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException) { return null; }

        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(material.PublicKey), out _);
        if (!rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            return null;

        var payload = JsonSerializer.Deserialize<Payload>(payloadBytes);
        if (payload is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(payload.S), Encoding.UTF8.GetBytes(serial)))
            return null;

        return payload.U;
    }

    private async Task<KeyMaterial> GetOrCreateMaterialAsync(CancellationToken cancellationToken)
    {
        var existing = await settings.GetAsync<KeyMaterial>(SettingKeys.HardwareKey, cancellationToken);
        if (existing is not null) return existing;

        using var rsa = RSA.Create(2048);
        var material = new KeyMaterial(
            Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
            secrets.Protect(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())));

        await settings.SetAsync(SettingKeys.HardwareKey, material, cancellationToken);
        return material;
    }
}
