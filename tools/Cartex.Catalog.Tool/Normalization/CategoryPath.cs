namespace Cartex.Catalog.Tool.Normalization;

public sealed record CategoryPath(string Parent, string Child, string? Issue, string? Detail)
{
    public const string DepthCode = "GKAT-40";
    public const string ShapeCode = "GKAT-41";
    public const string UnclassifiedCode = "GKAT-44";

    private const string Unclassified = "Boshqa";

    public string Leaf => Child.Length > 0 ? Child : Parent;

    public static CategoryPath Split(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var separator = text.IndexOf('/', StringComparison.Ordinal);
        if (separator < 0)
            return Of(text, string.Empty);

        var parent = text[..separator].Trim();
        var child = text[(separator + 1)..].Trim();
        return child.Contains('/', StringComparison.Ordinal)
            ? new CategoryPath(parent, child, DepthCode, "category is deeper than two levels")
            : Of(parent, child);
    }

    public static CategoryPath Of(string? parent, string? child)
    {
        var levels = new[] { parent, child }.Select(level => (level ?? string.Empty).Trim()).ToList();
        var unclassified = levels.Any(level => level.Equals(Unclassified, StringComparison.OrdinalIgnoreCase));
        var known = levels.Where(level => level.Length > 0 && !level.Equals(Unclassified, StringComparison.OrdinalIgnoreCase)).ToList();

        return known.Count switch
        {
            0 => new CategoryPath(string.Empty, string.Empty,
                unclassified ? UnclassifiedCode : ShapeCode,
                unclassified ? $"'{Unclassified}' means unclassified, so the category is left empty" : "category is empty"),
            1 => new CategoryPath(known[0], string.Empty,
                unclassified ? UnclassifiedCode : ShapeCode,
                unclassified ? $"'{Unclassified}' means unclassified, so that level is left empty" : "category has only one level"),
            _ => new CategoryPath(known[0], known[1], null, null)
        };
    }
}
