using System.Text;

namespace Cartex.Catalog.Tool.Normalization;

public static class Transliterator
{
    private const string Apostrophes = "'ʻʼ’`";

    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    {
        "W", "kW", "kVA", "V", "A", "Ah", "mm", "cm", "m", "kg",
        "g", "l", "ml", "rpm", "min", "h", "J", "Nm", "bar", "C"
    };

    private static readonly Dictionary<string, string> Digraphs = new(StringComparer.Ordinal)
    {
        ["sh"] = "ш", ["ch"] = "ч", ["ng"] = "нг", ["yo"] = "ё",
        ["yu"] = "ю", ["ya"] = "я", ["ye"] = "е"
    };

    private static readonly Dictionary<char, string> Letters = new()
    {
        ['a'] = "а", ['b'] = "б", ['c'] = "с", ['d'] = "д", ['e'] = "е", ['f'] = "ф",
        ['g'] = "г", ['h'] = "ҳ", ['i'] = "и", ['j'] = "ж", ['k'] = "к", ['l'] = "л",
        ['m'] = "м", ['n'] = "н", ['o'] = "о", ['p'] = "п", ['q'] = "қ", ['r'] = "р",
        ['s'] = "с", ['t'] = "т", ['u'] = "у", ['v'] = "в", ['w'] = "в", ['x'] = "х",
        ['y'] = "й", ['z'] = "з"
    };

    public static string ToCyrillic(string name, string manufacturer, string model)
    {
        var reserved = Reserved(manufacturer, model);
        var result = new StringBuilder(name.Length * 2);
        foreach (var (text, word) in Split(name))
            result.Append(word && !Keep(text, reserved) ? Cyrillic(text) : text);

        return result.ToString();
    }

    private static HashSet<string> Reserved(string manufacturer, string model)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (text, word) in Split(manufacturer).Concat(Split(model)))
            if (word)
                reserved.Add(text);

        return reserved;
    }

    private static bool Keep(string token, HashSet<string> reserved) =>
        reserved.Contains(token) ||
        Units.Contains(token) ||
        token.Any(char.IsAsciiDigit) ||
        token.Skip(1).Any(char.IsUpper);

    private static string Cyrillic(string token)
    {
        var result = new StringBuilder(token.Length * 2);
        var index = 0;
        while (index < token.Length)
        {
            var upper = char.IsUpper(token[index]);
            if (TryApostrophe(token, index, out var pair))
            {
                result.Append(Cased(pair, upper));
                index += 2;
            }
            else if (Digraph(token, index) is { } digraph)
            {
                result.Append(Cased(digraph, upper));
                index += 2;
            }
            else
            {
                result.Append(Cased(Single(token, index), upper));
                index++;
            }
        }

        return result.ToString();
    }

    private static string Single(string token, int index)
    {
        var letter = char.ToLowerInvariant(token[index]);
        if (letter == 'e' && index == 0)
            return "э";

        return Letters.TryGetValue(letter, out var single) ? single : token[index].ToString();
    }

    private static bool TryApostrophe(string token, int index, out string letter)
    {
        letter = index + 1 < token.Length && Apostrophes.Contains(token[index + 1], StringComparison.Ordinal)
            ? char.ToLowerInvariant(token[index]) switch { 'o' => "ў", 'g' => "ғ", _ => string.Empty }
            : string.Empty;

        return letter.Length > 0;
    }

    private static string? Digraph(string token, int index) =>
        index + 1 < token.Length && !TryApostrophe(token, index + 1, out _)
            ? Digraphs.GetValueOrDefault(token.Substring(index, 2).ToLowerInvariant())
            : null;

    private static string Cased(string cyrillic, bool upper) =>
        upper ? char.ToUpperInvariant(cyrillic[0]) + cyrillic[1..] : cyrillic;

    private static IEnumerable<(string Text, bool Word)> Split(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var word = IsWord(text[start]);
            var end = start;
            while (end < text.Length && IsWord(text[end]) == word)
                end++;

            yield return (text[start..end], word);
            start = end;
        }
    }

    private static bool IsWord(char symbol) =>
        char.IsLetterOrDigit(symbol) || Apostrophes.Contains(symbol, StringComparison.Ordinal);
}
