using System.Globalization;
using System.Text;

namespace Cartex.Catalog.Tool.Normalization;

public static class SearchFold
{
    private const string Apostrophes = "'ʻʼ’`";

    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['ғ'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "yo", ['ж'] = "j", ['з'] = "z", ['и'] = "i", ['й'] = "y",
        ['к'] = "k", ['қ'] = "q", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['ў'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "x", ['ҳ'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh",
        ['щ'] = "sh", ['ъ'] = "", ['ы'] = "i", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya"
    };

    public static string Build(string text)
    {
        var latin = new StringBuilder(text.Length);
        foreach (var symbol in Homoglyphs.Clean(text).ToLowerInvariant())
            latin.Append(Transliteration.TryGetValue(symbol, out var replacement) ? replacement : symbol);

        var folded = new StringBuilder(latin.Length);
        foreach (var symbol in latin.ToString().Normalize(NormalizationForm.FormD))
        {
            if (Apostrophes.Contains(symbol, StringComparison.Ordinal) ||
                CharUnicodeInfo.GetUnicodeCategory(symbol) == UnicodeCategory.NonSpacingMark)
                continue;

            folded.Append(char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol) ? symbol : ' ');
        }

        return NameNormalizer.Collapse(folded.ToString());
    }
}
