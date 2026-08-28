using System.Globalization;

namespace Cartex.Catalog.Tool.Packaging;

public sealed record PackManifest(
    string ShopType,
    int Version,
    int RowCount,
    long ByteSize,
    string Sha256,
    string Signature,
    string BuiltAt,
    string ImageBaseUrl)
{
    public const string ImageBase = "https://catalog.cartex.uz/";

    public static PackManifest Create(string shopType, int version, int rowCount, long byteSize, byte[] hash, byte[] signature) =>
        new(shopType,
            version,
            rowCount,
            byteSize,
            Convert.ToBase64String(hash),
            Convert.ToBase64String(signature),
            DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ImageBase);
}
