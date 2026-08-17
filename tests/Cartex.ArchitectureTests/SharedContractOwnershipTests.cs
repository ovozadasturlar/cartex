using System.Text.RegularExpressions;
using Xunit;

namespace Cartex.ArchitectureTests;

/// A wire type declared in both `Cartex.Application` and `Cartex.Shared` compiles, serialises and
/// looks fine — until the two copies drift. The API then sends the Application shape while every
/// typed client deserialises the Shared one, and the extra field simply vanishes with no error
/// anywhere. That is exactly how `RoleDto.IsSystem` stopped reaching the desktop.
///
/// One name, one owner: the contract lives in `Cartex.Shared`, the Application layer uses it.
public partial class SharedContractOwnershipTests
{
    [GeneratedRegex(@"^public (?:(?:sealed |abstract )?record (?:class |struct )?|enum )(\w+)\b",
        RegexOptions.Multiline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Declaration();

    [Fact]
    public void No_contract_type_is_declared_in_both_Application_and_Shared()
    {
        var root = SolutionRoot.Find();
        var shared = Declarations(Path.Combine(root, "src", "shared", "Cartex.Shared"));
        var application = Declarations(Path.Combine(root, "src", "backend", "Cartex.Application"));

        var clashes = application
            .Where(x => shared.ContainsKey(x.Key))
            .Select(x => $"{x.Key}{Environment.NewLine}      Application: {Rel(root, x.Value)}"
                         + $"{Environment.NewLine}      Shared     : {Rel(root, shared[x.Key])}")
            .Order()
            .ToList();

        Assert.True(clashes.Count == 0,
            $"These contract types are declared twice. Delete the Cartex.Application copy and use "
            + $"the Cartex.Shared one, so the wire shape and the client shape cannot drift:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", clashes));
    }

    private static Dictionary<string, string> Declarations(string root)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            foreach (Match match in Declaration().Matches(File.ReadAllText(file)))
                found.TryAdd(match.Groups[1].Value, file);
        }
        return found;
    }

    private static string Rel(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
