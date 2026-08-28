using System.Text.RegularExpressions;

namespace Cartex.Catalog.Tool.Normalization;

public sealed record NormalizedName(string Value, string? ReviewReason);

public static class NameNormalizer
{
    public const int MaxLength = 80;
    public const string ReviewCode = "GKAT-20-review";
    public const string LengthCode = "GKAT-23";

    private const int TypeScan = 4;

    private static readonly Dictionary<string, string> Units = new(StringComparer.Ordinal)
    {
        ["об/мин"] = "rpm",
        ["кВт"] = "kW",
        ["кВА"] = "kVA",
        ["Вт"] = "W",
        ["Ач"] = "Ah",
        ["мин"] = "min",
        ["час"] = "h",
        ["бар"] = "bar",
        ["мм"] = "mm",
        ["см"] = "cm",
        ["кг"] = "kg",
        ["Дж"] = "J",
        ["Нм"] = "Nm",
        ["шт"] = "dona",
        ["мл"] = "ml",
        ["гр"] = "g",
        ["В"] = "V",
        ["А"] = "A",
        ["м"] = "m",
        ["г"] = "g",
        ["л"] = "l"
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Размер"] = "oʻlcham",
        ["толщина"] = "qalinlik",
        ["зубьев"] = "tish",
        ["Зернистость"] = "donadorlik",
        ["напор"] = "bosim",
        ["вес"] = "ogʻirlik"
    };

    private static readonly Regex UnitPattern = new(
        $@"(?<=[\d/])(\s*)({string.Join('|', Units.Keys.OrderByDescending(unit => unit.Length))})(?!\p{{IsCyrillic}})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex LabelPattern = new(
        $@"\b({string.Join('|', Labels.Keys)})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly Regex Whitespace = new(@"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex FinishCode = new(@"^[A-Z]{2,3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string ReplaceUnits(string name) =>
        UnitPattern.Replace(name, match => match.Groups[1].Value + Units[match.Groups[2].Value]);

    public static string ReplaceLabels(string name) =>
        LabelPattern.Replace(name, match => Labels[match.Value]);

    public static string Collapse(string name) => Whitespace.Replace(name, " ").Trim();

    public static string Clean(string name) => Collapse(Homoglyphs.Clean(ReplaceLabels(ReplaceUnits(name))));

    public static NormalizedName Normalize(string text, string manufacturer, string categoryLeaf, TypeVocabulary types)
    {
        if (text.Length == 0)
            return new NormalizedName(text, "name is empty");

        var (head, parameters) = SplitParameters(text);
        var words = head.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var brand = manufacturer.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (brand.Length > 0)
        {
            var occurrences = Occurrences(words, brand);
            if (occurrences.Count == 0)
                return new NormalizedName(text, "manufacturer does not appear in the name");

            words = KeepFirstBrand(words, occurrences, brand.Length);
            words.RemoveRange(occurrences[0], brand.Length);
        }

        var typeAt = words.FindIndex(0, Math.Min(TypeScan, words.Count), types.Recognizes);
        if (typeAt < 0)
            return new NormalizedName(text, "the name carries no type word");

        var (start, length) = types.Portion(words, typeAt);
        var lead = words.Take(start).ToList();
        var codes = lead.Where(IsFinish).ToList();
        var qualifiers = lead.Where(word => !IsModel(word) && !IsFinish(word)).ToList();
        var type = types.Resolve([.. qualifiers, .. words.Skip(start).Take(length)], categoryLeaf);
        if (type.ReviewReason is not null)
            return new NormalizedName(text, type.ReviewReason);

        var ordered = type.Words.Concat(brand).Concat(lead.Where(IsModel)).Concat(words.Skip(start + length)).ToList();
        var carried = words.Where((_, i) => i < start || i >= start + length).Concat(brand);
        if (!Carries(ordered.Concat(codes), carried, type.Words.Skip(qualifiers.Count)))
            return new NormalizedName(text, "reordering would not carry every word of the name exactly once");

        var built = Join(string.Join(' ', ordered), Extend(parameters, codes));
        return built.Length > MaxLength && text.Length <= MaxLength
            ? new NormalizedName(text, "reordering would push the name past the length limit")
            : new NormalizedName(built, null);
    }

    public static string Shorten(string name)
    {
        if (name.Length <= MaxLength)
            return name;

        var (head, parameters) = SplitParameters(name);
        if (parameters.Length == 0)
            return name;

        var parts = parameters[1..^1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        while (parts.Count > 0)
        {
            parts.RemoveAt(parts.Count - 1);
            var candidate = parts.Count == 0 ? head : $"{head} ({string.Join(", ", parts)})";
            if (candidate.Length <= MaxLength)
                return candidate;
        }

        return head;
    }

    private static bool IsModel(string word) => word.Any(char.IsAsciiDigit);

    private static bool IsFinish(string word) => !IsModel(word) && FinishCode.IsMatch(word);

    private static (string Head, string Parameters) SplitParameters(string text)
    {
        var open = text.IndexOf('(', StringComparison.Ordinal);
        return open > 0 && text.EndsWith(')')
            ? (text[..open].Trim(), text[open..])
            : (text, string.Empty);
    }

    private static string Join(string head, string parameters) =>
        parameters.Length == 0 ? head : $"{head} {parameters}";

    private static string Extend(string parameters, IReadOnlyList<string> codes) =>
        codes.Count == 0 ? parameters
        : parameters.Length == 0 ? $"({string.Join(", ", codes)})"
        : $"{parameters[..^1]}, {string.Join(", ", codes)})";

    private static bool Carries(IEnumerable<string> produced, IEnumerable<string> carried, IEnumerable<string> type)
    {
        var want = Tally(carried);
        var named = Tally(type);
        if (named.Keys.Any(want.ContainsKey))
            return false;

        var made = Tally(produced);
        return made.Count == want.Count + named.Count
            && want.Concat(named).All(word => made.GetValueOrDefault(word.Key) == word.Value);
    }

    private static Dictionary<string, int> Tally(IEnumerable<string> words)
    {
        var tally = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words.SelectMany(word => word.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            tally[word] = tally.GetValueOrDefault(word) + 1;

        return tally;
    }

    private static List<int> Occurrences(IReadOnlyList<string> words, string[] brand)
    {
        var found = new List<int>();
        for (var i = 0; i + brand.Length <= words.Count; i++)
        {
            var matched = true;
            for (var offset = 0; offset < brand.Length && matched; offset++)
                matched = words[i + offset].Equals(brand[offset], StringComparison.OrdinalIgnoreCase);

            if (!matched)
                continue;

            found.Add(i);
            i += brand.Length - 1;
        }

        return found;
    }

    private static List<string> KeepFirstBrand(IReadOnlyList<string> words, List<int> occurrences, int length) =>
        words.Where((_, i) => !occurrences.Skip(1).Any(start => i >= start && i < start + length)).ToList();
}
