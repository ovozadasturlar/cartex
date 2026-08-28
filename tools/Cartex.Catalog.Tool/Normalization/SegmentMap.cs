namespace Cartex.Catalog.Tool.Normalization;

public static class SegmentMap
{
    public const string Column = "segments";

    private const char Separator = ';';

    private static readonly Dictionary<string, string[]> ByParent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Elektr mollari"] = ["elektr"],
        ["Elektr asboblar"] = ["asbob"],
        ["Qo'l asboblari"] = ["asbob"],
        ["Nasadka va disklar"] = ["asbob"],
        ["O'lchov asboblari"] = ["asbob"],
        ["Santexnika"] = ["santexnika"],
        ["Qurilish mollari"] = ["qurilish"]
    };

    public static string[] For(string categoryParent) => ByParent.GetValueOrDefault(categoryParent, []);

    public static string Join(string categoryParent) => string.Join(Separator, For(categoryParent));

    public static string[] Split(string cell) =>
        cell.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string[] Resolve(string cell, string categoryParent) =>
        Split(cell) is { Length: > 0 } segments ? segments : For(categoryParent);
}
