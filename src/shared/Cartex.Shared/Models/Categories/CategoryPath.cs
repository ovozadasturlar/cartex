namespace Cartex.Shared.Models.Categories;

public static class CategoryPath
{
    public const string Separator = " / ";

    public static string Build(params string?[] segments) =>
        string.Join(Separator, segments.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static string Truncate(string fullPath, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;
        if (fullPath.Length <= maxLength) return fullPath;
        if (maxLength == 1) return "…";

        var segments = fullPath.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) return fullPath[^maxLength..];

        var suffix = segments[^1];
        if (suffix.Length + Separator.Length + 1 > maxLength)
            return "…" + suffix[^(maxLength - 1)..];

        for (var index = segments.Length - 2; index >= 0; index--)
        {
            var candidate = segments[index] + Separator + suffix;
            if (candidate.Length + Separator.Length + 1 > maxLength) break;
            suffix = candidate;
        }

        return "…" + Separator + suffix;
    }
}
