namespace Cartex.Catalog.Tool.Normalization;

public sealed record ManufacturerConflict(string First, string Second, string Reason);

public sealed class ManufacturerIndex
{
    public const string RecoveredCode = "GKAT-90";

    private const int LongNameLength = 6;
    private const int ContainedLength = 5;

    private static readonly (string From, string To)[] PhoneticFolds =
    [
        ("ck", "k"), ("ph", "f"), ("c", "k"), ("ss", "s"), ("y", "i"), ("w", "v"), ("x", "ks")
    ];

    private readonly Dictionary<string, string> canonical;
    private readonly (string[] Words, string Name)[] aliases;

    private ManufacturerIndex(Dictionary<string, string> canonical, IReadOnlyList<ManufacturerConflict> conflicts)
    {
        this.canonical = canonical;
        aliases = canonical
            .Select(entry => (Words: entry.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries), Name: entry.Value))
            .OrderByDescending(alias => alias.Words.Length)
            .ToArray();
        Conflicts = conflicts;
    }

    public IReadOnlyList<ManufacturerConflict> Conflicts { get; }

    public IReadOnlyList<string> Names => canonical.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    public string Canonical(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length > 0 && canonical.TryGetValue(text, out var name) ? name : text;
    }

    public string Recover(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var found = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            var alias = aliases.FirstOrDefault(candidate => Matches(words, i, candidate.Words));
            if (alias.Name is null)
                continue;

            found.Add(alias.Name);
            i += alias.Words.Length - 1;
        }

        return found.Distinct(StringComparer.Ordinal).Count() == 1 ? found[0] : string.Empty;
    }

    public static ManufacturerIndex Build(IEnumerable<string?> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length > 0)
                counts[text] = counts.GetValueOrDefault(text) + 1;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in counts.GroupBy(entry => entry.Key.ToLowerInvariant(), StringComparer.Ordinal))
        {
            var winner = group.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal).First().Key;
            foreach (var entry in group)
                map[entry.Key] = winner;
        }

        var names = map.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var conflicts = new List<ManufacturerConflict>();
        for (var i = 0; i < names.Count; i++)
            for (var j = i + 1; j < names.Count; j++)
            {
                var reason = NearDuplicate(names[i], names[j]);
                if (reason is not null)
                    conflicts.Add(new ManufacturerConflict(names[i], names[j], reason));
            }

        return new ManufacturerIndex(map, conflicts);
    }

    public static string? NearDuplicate(string first, string second)
    {
        var left = Fold(first);
        var right = Fold(second);
        if (Distance(left, right) <= Tolerance(left, right))
            return "edit distance";

        var leftWords = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightWords = second.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (leftWords.Length != rightWords.Length)
        {
            var shared = Math.Min(leftWords.Length, rightWords.Length);
            if (leftWords.Take(shared).SequenceEqual(rightWords.Take(shared), StringComparer.OrdinalIgnoreCase))
                return "trailing word";

            var leftHead = Fold(leftWords[0]);
            var rightHead = Fold(rightWords[0]);
            if (Distance(leftHead, rightHead) <= Tolerance(leftHead, rightHead))
                return "first word";
        }

        return Math.Min(left.Length, right.Length) >= ContainedLength
               && (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
            ? "one name contains the other"
            : null;
    }

    private static bool Matches(string[] words, int start, string[] alias)
    {
        if (start + alias.Length > words.Length)
            return false;

        for (var offset = 0; offset < alias.Length; offset++)
            if (!words[start + offset].Equals(alias[offset], StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }

    private static int Tolerance(string left, string right) =>
        Math.Min(left.Length, right.Length) >= LongNameLength ? 2 : 1;

    private static string Fold(string value)
    {
        var letters = string.Concat(value.ToLowerInvariant().Where(char.IsAsciiLetterLower));
        foreach (var (from, to) in PhoneticFolds)
            letters = letters.Replace(from, to, StringComparison.Ordinal);

        return letters;
    }

    private static int Distance(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
            return 0;

        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
