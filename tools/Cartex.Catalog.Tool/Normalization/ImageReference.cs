namespace Cartex.Catalog.Tool.Normalization;

public sealed record ImageReference(string Value, string? Issue, string? Detail)
{
    public const string RejectedCode = "GKAT-61";

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cartex.xonqiz.uz",
        "veral.dusel.uz",
        "images.epa.uz"
    };

    public static ImageReference FromUrl(string? url)
    {
        var text = (url ?? string.Empty).Trim();
        if (text.Length == 0)
            return new ImageReference(string.Empty, null, null);

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && AllowedHosts.Contains(uri.Host)
            ? new ImageReference(text, null, null)
            : new ImageReference(string.Empty, RejectedCode, $"image source {Host(text)} is not one we hold the rights to");
    }

    public static ImageReference FromKey(string? key) =>
        new((key ?? string.Empty).Trim(), null, null);

    private static string Host(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unparsable url";
}
