namespace Cartex.Shared.Barcodes;

public static class WeightedBarcode
{
    private const int TotalLength = 13;
    private const int ProductCodeLength = 5;
    private const int EmbeddedValueLength = 5;
    private const int ChecksumLength = 1;
    private const decimal GramsPerKilogram = 1000m;
    private static readonly string[] Prefixes = ["02", "2"];

    public static bool TryParse(string raw, out string productCode, out decimal quantity)
    {
        productCode = string.Empty;
        quantity = 0;
        var code = raw.Trim();
        if (code.Length != TotalLength || !code.All(char.IsAsciiDigit))
            return false;
        if (Prefixes.FirstOrDefault(code.StartsWith) is not { } prefix)
            return false;
        var embedded = code.AsSpan(TotalLength - ChecksumLength - EmbeddedValueLength, EmbeddedValueLength);
        if (!decimal.TryParse(embedded, out var grams) || grams <= 0)
            return false;
        productCode = code.Substring(prefix.Length, ProductCodeLength);
        quantity = grams / GramsPerKilogram;
        return true;
    }
}
