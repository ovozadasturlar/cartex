using Cartex.Catalog.Tool.Io;

namespace Cartex.Catalog.Tool.Normalization;

public sealed record ResolvedType(IReadOnlyList<string> Words, string? ReviewReason);

public sealed class TypeVocabulary
{
    public const string DefaultPath = "docs/catalog/types.csv";

    private const string WordColumn = "soz";
    private const string TypeColumn = "tur_nomi";
    private const string DecisionColumn = "qaror";
    private const string TypeDecision = "tur";
    private const int MinimumStem = 4;

    private static readonly string[] Required = [WordColumn, TypeColumn, DecisionColumn];
    private static readonly string[] Generic = ["asbob", "mahsulot", "tovar", "o'lchov asbobi"];
    private static readonly string[] Suffixes = ["lari", "leri", "lar", "ler", "si", "i"];
    private static readonly (string Suffix, string Ending)[] Plurals =
        [("lari", "i"), ("leri", "i"), ("lar", ""), ("ler", "")];
    private const string Vowels = "aeiouʻ'";

    private readonly Dictionary<string, string> types;
    private readonly (string Key, int Length)[] phrases;

    private TypeVocabulary(Dictionary<string, string> types)
    {
        this.types = types;
        phrases =
        [
            .. Generic
                .Select(entry => entry.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(entry => entry.Length > 1)
                .OrderByDescending(entry => entry.Length)
                .Select(entry => (Key: Key(entry), entry.Length))
        ];
    }

    public static TypeVocabulary Load(string path)
    {
        var records = CsvFile.Read(path);
        if (records.Count == 0)
            throw new InvalidDataException($"'{path}' holds no header row.");

        var columns = CsvFile.Columns(path, records[0], Required);
        var vocabulary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Skip(1))
        {
            var word = CsvFile.Value(record, columns, WordColumn);
            var name = CsvFile.Value(record, columns, TypeColumn);
            if (word.Length > 0 && name.Length > 0 &&
                CsvFile.Value(record, columns, DecisionColumn).Equals(TypeDecision, StringComparison.OrdinalIgnoreCase))
                vocabulary[word] = vocabulary[name] = name;
        }

        return vocabulary.Count > 0
            ? new TypeVocabulary(vocabulary)
            : throw new InvalidDataException($"'{path}' declares no '{TypeDecision}' row, so no name can be ordered (GKAT-26).");
    }

    public bool Recognizes(string word) => Known(Stem(word));

    public (int Start, int Length) Portion(IReadOnlyList<string> words, int at)
    {
        foreach (var (key, length) in phrases)
            for (var start = Math.Max(0, at - length + 1); start <= at && start + length <= words.Count; start++)
                if (string.Equals(Key([.. words.Skip(start).Take(length)]), key, StringComparison.Ordinal))
                    return (start, length);

        return (at, IsGeneric(words[at]) && at + 1 < words.Count && IsType(words[at + 1]) ? 2 : 1);
    }

    public ResolvedType Resolve(IReadOnlyList<string> words, string categoryLeaf)
    {
        var head = words[^1];
        var stem = Stem(head);
        if (IsGeneric(words) || IsGeneric(head))
        {
            var derived = Singular(categoryLeaf);
            return derived.Length == 0
                ? new ResolvedType(words, $"the type '{string.Join(' ', words)}' is generic and the category names no leaf to replace it")
                : new ResolvedType(derived.Split(' '), null);
        }

        if (types.TryGetValue(head, out var name))
            return new ResolvedType(Substituted([.. words.Take(words.Count - 1), Cased(name, words.Count == 1)], categoryLeaf), null);

        return types.ContainsKey(stem)
            ? new ResolvedType(Substituted(words, categoryLeaf), null)
            : new ResolvedType(words, $"'{head}' is not in the type vocabulary");
    }

    private IReadOnlyList<string> Substituted(IReadOnlyList<string> words, string categoryLeaf)
    {
        var derived = words.Any(IsGeneric) ? Singular(categoryLeaf) : string.Empty;
        if (derived.Length == 0)
            return words;

        var named = derived.Split(' ').Select(Stem).ToArray();
        var kept = new List<string>(words.Count);
        foreach (var word in words)
            if (IsGeneric(word))
                kept.Add(Cased(derived, kept.Count == 0));
            else if (!named.Contains(Stem(word), StringComparer.OrdinalIgnoreCase))
                kept.Add(word);

        return kept;
    }

    private string Stem(string word)
    {
        if (Known(word))
            return word;

        foreach (var suffix in Suffixes)
        {
            if (!word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var stem = word[..^suffix.Length];
            if (stem.Length >= MinimumStem && Known(stem))
                return stem;
        }

        return word;
    }

    private bool Known(string word) =>
        types.ContainsKey(word) || Generic.Contains(word, StringComparer.OrdinalIgnoreCase);

    private bool IsType(string word) => types.ContainsKey(Stem(word));

    private bool IsGeneric(string word) => Generic.Contains(Stem(word), StringComparer.OrdinalIgnoreCase);

    private bool IsGeneric(IReadOnlyList<string> words)
    {
        var key = Key(words);
        return phrases.Any(phrase => string.Equals(phrase.Key, key, StringComparison.Ordinal));
    }

    private static string Key(IReadOnlyList<string> words) =>
        SearchFold.Build(string.Join(' ', words.SkipLast(1).Append(Root(words[^1]))));

    private static string Root(string word)
    {
        foreach (var suffix in Suffixes)
            if (word.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && word.Length - suffix.Length >= MinimumStem)
                return word[..^suffix.Length];

        return word;
    }

    private static string Singular(string categoryLeaf)
    {
        var words = categoryLeaf.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        var last = words[^1];
        var plural = Plurals.FirstOrDefault(entry =>
            last.Length > entry.Suffix.Length && last.EndsWith(entry.Suffix, StringComparison.OrdinalIgnoreCase));

        if (plural.Suffix is not null)
        {
            var stem = last[..^plural.Suffix.Length];
            words[^1] = stem + (plural.Ending.Length > 0 && Vowels.Contains(char.ToLowerInvariant(stem[^1]))
                ? "si"
                : plural.Ending);
        }

        return string.Join(' ', words);
    }

    private static string Cased(string name, bool leading) =>
        leading ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
