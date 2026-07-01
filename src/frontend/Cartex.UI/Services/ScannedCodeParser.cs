namespace Cartex.UI.Services;

public enum ScannedCodeType { Plain, Weighted }

public record ScannedCode(ScannedCodeType Type, string Code, decimal? Weight = null, decimal? Price = null);

public interface IScannedCodeParser
{
    ScannedCode Parse(string raw);
}

public sealed class ScannedCodeParser : IScannedCodeParser
{
    private const int EanLength = 13;
    private const int PluLength = 5;
    private const int EmbeddedValueLength = 5;
    private const int ChecksumLength = 1;
    private const decimal GramsPerKilogram = 1000m;
    private static readonly string[] WeightedPrefixes = ["02", "2"];

    public ScannedCode Parse(string raw)
    {
        var code = raw.Trim();
        if (code.Length == EanLength && code.All(char.IsDigit)
            && WeightedPrefixes.FirstOrDefault(code.StartsWith) is { } prefix)
        {
            var plu = code.Substring(prefix.Length, PluLength);
            var embedded = code.Substring(EanLength - ChecksumLength - EmbeddedValueLength, EmbeddedValueLength);
            if (decimal.TryParse(embedded, out var grams))
                return new ScannedCode(ScannedCodeType.Weighted, plu, grams / GramsPerKilogram);
        }
        return new ScannedCode(ScannedCodeType.Plain, code);
    }
}
