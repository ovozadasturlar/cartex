using System.Text.RegularExpressions;

namespace Cartex.Catalog.Tool.Normalization;

public static class Homoglyphs
{
    private const string Cyrillic = "АВЕКМНОРСТХУавекмнорстху";
    private const string Latin = "ABEKMHOPCTXYabekmhopctxy";

    private static readonly Regex Token = new(@"[\p{L}\p{Nd}'ʻʼ]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string Clean(string text) => Token.Replace(text, match => Convert(match.Value));

    private static string Convert(string token) =>
        token.Any(symbol => char.IsAsciiLetter(symbol) || char.IsAsciiDigit(symbol))
            ? string.Concat(token.Select(Latinize))
            : token;

    private static char Latinize(char symbol)
    {
        var index = Cyrillic.IndexOf(symbol, StringComparison.Ordinal);
        return index < 0 ? symbol : Latin[index];
    }
}
