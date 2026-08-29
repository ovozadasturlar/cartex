using System.Text.RegularExpressions;
using Cartex.Domain.Authorization;
using Xunit;

namespace Cartex.ArchitectureTests;

public partial class ClientPermissionTests
{
    private const int MinimumScanned = 80;

    private static readonly string[] ClientRoots =
    [
        Path.Combine("src", "mobile"),
        Path.Combine("src", "desktop"),
        Path.Combine("src", "web", "Cartex.Web.Angular", "src")
    ];

    private static readonly string[] SourcePatterns = ["*.cs", "*.xaml", "*.axaml", "*.ts", "*.html"];

    [GeneratedRegex("""(?:(?:permissions|perms)\.Has(?:Any)?|\.[Hh]asPermission)\(\s*((?:["'][^"']+["']\s*,?\s*)+)\)""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PermissionCall();

    [GeneratedRegex("""["']([^"']+)["']""", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal();

    [Fact]
    public void Client_permission_strings_exist_in_the_catalog()
    {
        var used = Scan();

        Assert.True(used.Count >= MinimumScanned,
            $"Only {used.Count} permission strings were found in the clients; the scanner no longer matches how they check permissions.");

        var unknown = used
            .Where(entry => !AppPermissions.Catalog.ContainsKey(entry.Key))
            .Select(entry => $"{entry.Key} <- {string.Join(", ", entry.Value.Order())}")
            .Order()
            .ToList();

        Assert.Empty(unknown);
    }

    private static Dictionary<string, SortedSet<string>> Scan()
    {
        var root = SolutionRoot.Find();
        var used = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        var files = ClientRoots
            .Select(relative => Path.Combine(root, relative))
            .Where(Directory.Exists)
            .SelectMany(directory => SourcePatterns.SelectMany(
                pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)))
            .Where(IsHandWritten);

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file);
            foreach (Match call in PermissionCall().Matches(File.ReadAllText(file)))
            foreach (Match literal in Literal().Matches(call.Groups[1].Value))
            foreach (var alternative in literal.Groups[1].Value.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var key = alternative.Trim();
                if (key.Length == 0 || key == AppPermissions.Wildcard) continue;
                if (!used.TryGetValue(key, out var sources))
                    used[key] = sources = new SortedSet<string>(StringComparer.Ordinal);
                sources.Add(relative);
            }
        }

        return used;
    }

    private static bool IsHandWritten(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
        && !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}");
}
