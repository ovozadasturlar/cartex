using System.Text.RegularExpressions;
using Xunit;

namespace Cartex.ArchitectureTests;

public sealed partial class MobileViewModelAccessTests
{
    private static readonly string[] ForbiddenPatterns =
    [
        "permissions.Has(",
        "permissions.HasAny(",
        "_permissions.Has(",
        "_permissions.HasAny(",
        "perms.Has(",
        "perms.HasAny(",
        "access.Has(",
        "access.HasAny(",
        "_access.Has(",
        "_access.HasAny(",
        "features.",
        "_features.",
        "access.Feature(",
        "_access.Feature("
    ];

    [GeneratedRegex(@"\bclass\s+(?<name>\w+ViewModel)\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ViewModelClass();

    [Fact]
    public void RUXSAT_04_Mobile_ViewModel_constructors_do_not_capture_access_state()
    {
        var root = SolutionRoot.Find();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "mobile"), "*ViewModel.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        var violations = files.SelectMany(path => FindViolations(File.ReadAllText(path), Path.GetRelativePath(root, path))).ToList();

        Assert.True(violations.Count == 0,
            "Mobil ViewModel konstruktorida ruxsat yoki modul holati hisoblangan. " +
            "Hisoblanadigan xossa va AccessState.Changed ishlating:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void RUXSAT_04_Scanner_reports_file_line_and_remediation()
    {
        const string source = """
            public sealed class SampleViewModel
            {
                public SampleViewModel(MobilePermissions permissions)
                {
                    CanSell = permissions.Has("sales.create");
                }
            }
            """;

        var violation = Assert.Single(FindViolations(source, "SampleViewModel.cs"));

        Assert.Contains("SampleViewModel.cs:5", violation, StringComparison.Ordinal);
        Assert.Contains("AccessState.Changed", violation, StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindViolations(string source, string path)
    {
        foreach (Match classMatch in ViewModelClass().Matches(source))
        {
            var className = classMatch.Groups["name"].Value;
            var constructor = new Regex($@"\b(?:public|internal|protected|private)\s+{Regex.Escape(className)}\s*\(",
                RegexOptions.None, TimeSpan.FromSeconds(1));

            foreach (Match constructorMatch in constructor.Matches(source))
            {
                var openingBrace = FindOpeningBrace(source, constructorMatch.Index + constructorMatch.Length);
                if (openingBrace < 0) continue;
                var closingBrace = FindClosingBrace(source, openingBrace);
                if (closingBrace < 0) continue;

                var body = source[openingBrace..(closingBrace + 1)];
                foreach (var pattern in ForbiddenPatterns.Where(body.Contains))
                {
                    var offset = body.IndexOf(pattern, StringComparison.Ordinal);
                    var line = 1 + source.AsSpan(0, openingBrace + offset).Count('\n');
                    yield return $"{path}:{line} — '{pattern}' konstruktor ichida — hisoblanadigan xossa va AccessState.Changed ishlating.";
                }
            }
        }
    }

    private static int FindOpeningBrace(string source, int start)
    {
        var parentheses = 1;
        for (var index = start; index < source.Length; index++)
        {
            if (source[index] == '(') parentheses++;
            else if (source[index] == ')' && --parentheses == 0)
                return source.IndexOf('{', index);
        }
        return -1;
    }

    private static int FindClosingBrace(string source, int openingBrace)
    {
        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index;
        }
        return -1;
    }
}
