using System.Text.RegularExpressions;
using Xunit;

namespace Cartex.ArchitectureTests;

public partial class AuditActionLengthTests
{
    private const int MaxLength = 15;

    [GeneratedRegex("""audit\.Add\(\s*"(?<action>[^"]+)"|Add\(\s*"(?<action>[^"]+)"\s*,\s*"[^"]+"\s*,""")]
    private static partial Regex AuditCall();

    [Fact]
    public void Audit_actions_fit_the_column()
    {
        var root = SolutionRoot();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(file => AuditCall()
                .Matches(File.ReadAllText(file))
                .Select(m => m.Groups["action"].Value)
                .Where(action => action.Length > MaxLength)
                .Select(action => $"{Path.GetFileName(file)}: \"{action}\" ({action.Length})"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cartex.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }
}
