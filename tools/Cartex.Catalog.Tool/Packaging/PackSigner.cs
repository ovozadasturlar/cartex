using System.Security.Cryptography;

namespace Cartex.Catalog.Tool.Packaging;

public sealed record SigningKeys(string PrivatePem, string PublicPem);

public static class PackSigner
{
    public const string PrivateKeyFile = "catalog-signing.pem";
    public const string PublicKeyFile = "catalog-signing.pub.pem";

    private const int KeySize = 256;

    public static SigningKeys Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new SigningKeys(key.ExportPkcs8PrivateKeyPem(), key.ExportSubjectPublicKeyInfoPem());
    }

    public static byte[] Hash(string path)
    {
        using var content = File.OpenRead(path);
        return SHA256.HashData(content);
    }

    public static ECDsa Load(string keyPath)
    {
        var pem = File.ReadAllText(keyPath);
        if (!pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidDataException($"'{keyPath}' holds no private key; sign with the '{PrivateKeyFile}' that keygen wrote.");

        var key = ECDsa.Create();
        key.ImportFromPem(pem);
        if (key.KeySize == KeySize)
            return key;

        var size = key.KeySize;
        key.Dispose();
        throw new InvalidDataException($"'{keyPath}' holds a {size}-bit key; the pack signature requires ECDSA P-256.");
    }
}
