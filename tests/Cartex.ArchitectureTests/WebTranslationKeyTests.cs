using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Cartex.ArchitectureTests;

/// The four language files being consistent with each other says nothing about whether a key the
/// web client asks for exists at all. A missing one does not throw — Transloco renders the key
/// itself, so the user sees `new_customer` on a button. That is how one slipped through.
public partial class WebTranslationKeyTests
{
    /// Only a call whose argument is a complete literal — `t('key')` — is checked. A key built at
    /// runtime (`t('cart_status_' + row.status)`) is followed by an operator instead of `)`, and
    /// nothing here can resolve what it becomes.
    [GeneratedRegex(@"\bt\(\s*'([a-z0-9_.]+)'\s*[),]", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TranslateCall();

    [Fact]
    public void Every_key_the_web_client_asks_for_exists()
    {
        var root = SolutionRoot.Find();
        var web = Path.Combine(root, "src", "web", "Cartex.Web.Angular");
        var app = Path.Combine(web, "src", "app");

        // The web dictionary is the desktop base plus the web-only additions, exactly as
        // scripts/copy-i18n.js merges them; checking against the desktop file alone would call
        // every page subtitle missing.
        var known = Keys(Path.Combine(root, "src", "desktop", "Cartex.UI", "Assets", "Languages", "uz-latn.json"));
        known.UnionWith(Keys(Path.Combine(web, "src", "i18n", "uz-latn.json")));

        var missing = Directory
            .EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".html", StringComparison.Ordinal))
            .SelectMany(file => TranslateCall()
                .Matches(File.ReadAllText(file))
                .Select(m => (Key: m.Groups[1].Value, File: Path.GetFileName(file))))
            .Where(x => !known.Contains(x.Key))
            .Select(x => $"{x.Key} ({x.File})")
            .Distinct()
            .Order()
            .ToList();

        Assert.True(missing.Count == 0,
            $"The web client uses keys that no language file defines. Add them to "
            + $"src/web/Cartex.Web.Angular/src/i18n (web-only) or to the desktop base; "
            + $"public/i18n is generated and any edit there is lost:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", missing));
    }

    private static HashSet<string> Keys(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!.Keys.ToHashSet()
        : [];
}
