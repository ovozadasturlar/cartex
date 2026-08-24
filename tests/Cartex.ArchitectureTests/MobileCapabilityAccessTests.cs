using System.Text.RegularExpressions;
using Xunit;

namespace Cartex.ArchitectureTests;

public sealed partial class MobileCapabilityAccessTests
{
    [GeneratedRegex(@"\b(?:Has|HasAny)\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PermissionCheck();

    [GeneratedRegex(@"\bFeature\s*\(|\b[A-Za-z_]\w*Enabled\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FeatureCheck();

    [GeneratedRegex("""//[^\r\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'""", RegexOptions.Singleline,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CSharpTrivia();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XamlComments();

    [GeneratedRegex(@"<.*?>", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XamlTag();

    [Fact]
    public void RUXSAT_04a_Mobile_ViewModels_and_XAML_do_not_combine_permission_and_feature_checks()
    {
        var root = SolutionRoot.Find();
        var mobileRoot = Path.Combine(root, "src", "mobile");
        var files = Directory.EnumerateFiles(mobileRoot, "*ViewModel.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(mobileRoot, "*.xaml", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path));

        var violations = files
            .SelectMany(path => FindViolations(File.ReadAllText(path), Path.GetRelativePath(root, path)))
            .ToList();

        Assert.True(violations.Count == 0,
            "Mobil ViewModel yoki XAML ruxsat va feature tekshiruvini bitta ifodada kombinatsiyalagan. " +
            "Bunday qoida faqat AccessState ichida bo'lishi mumkin:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void RUXSAT_04a_Scanner_reports_file_line_and_named_capability_remediation()
    {
        const string source = """
            public sealed class SampleViewModel
            {
                public bool CanSell =>
                    access.HasAny("sales.create", "sales.checkout") &&
                    access.Feature("ordering");
            }
            """;

        var violation = Assert.Single(FindViolations(source, "SampleViewModel.cs"));

        Assert.Contains("SampleViewModel.cs:4", violation, StringComparison.Ordinal);
        Assert.Contains("buni AccessState ga nomlangan imkoniyat sifatida ko'chir", violation,
            StringComparison.Ordinal);
        Assert.Empty(FindViolations(source, "AccessState.cs"));
    }

    [Fact]
    public void RUXSAT_04a_XAML_scanner_reports_file_line_and_named_capability_remediation()
    {
        const string source = """
            <ContentPage>
                <Button
                    Text="Cart"
                    IsVisible="{Binding Access.Has(sales.create) &amp;&amp; Access.CartsEnabled}" />
            </ContentPage>
            """;

        var violation = Assert.Single(FindViolations(source, "ScanView.xaml"));

        Assert.Contains("ScanView.xaml:4", violation, StringComparison.Ordinal);
        Assert.Contains("buni AccessState ga nomlangan imkoniyat sifatida ko'chir", violation,
            StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindViolations(string source, string path)
    {
        if (Path.GetFileName(path).Equals("AccessState.cs", StringComparison.OrdinalIgnoreCase))
            yield break;

        var isXaml = Path.GetExtension(path).Equals(".xaml", StringComparison.OrdinalIgnoreCase);
        var searchable = Mask(source, isXaml ? XamlComments() : CSharpTrivia());
        var expressions = isXaml ? XamlExpressions(searchable) : CSharpExpressions(searchable);

        foreach (var (start, expression) in expressions)
        {
            var permission = PermissionCheck().Match(expression);
            var feature = FeatureCheck().Match(expression);
            if (!permission.Success || !feature.Success) continue;

            var offset = start + Math.Min(permission.Index, feature.Index);
            var line = 1 + source.AsSpan(0, offset).Count('\n');
            yield return $"{path}:{line} — ruxsat va feature tekshiruvi bitta ifodada kombinatsiyalangan; " +
                         "buni AccessState ga nomlangan imkoniyat sifatida ko'chir.";
        }
    }

    private static IEnumerable<(int Start, string Text)> CSharpExpressions(string source)
    {
        var start = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] is not (';' or '{' or '}')) continue;

            yield return (start, source[start..index]);
            start = index + 1;
        }

        if (start < source.Length)
            yield return (start, source[start..]);
    }

    private static IEnumerable<(int Start, string Text)> XamlExpressions(string source)
    {
        foreach (Match match in XamlTag().Matches(source))
            yield return (match.Index, match.Value);
    }

    private static string Mask(string source, Regex pattern) => pattern.Replace(source, match =>
        string.Concat(match.Value.Select(character => character is '\r' or '\n' ? character : ' ')));

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
