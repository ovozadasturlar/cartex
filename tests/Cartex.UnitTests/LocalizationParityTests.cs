using System.Text.Json;
using Cartex.Domain.Authorization;
using Cartex.Domain.Enums;
using Xunit;

namespace Cartex.UnitTests;

public class LocalizationParityTests
{
    private static readonly string[] Files = ["uz-latn.json", "uz-cyrl.json", "ru.json", "en.json"];

    private static Dictionary<string, string> Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Languages", file);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

    [Fact]
    public void All_language_files_share_identical_key_sets()
    {
        var reference = Load(Files[0]).Keys.ToHashSet();
        foreach (var file in Files.Skip(1))
        {
            var keys = Load(file).Keys.ToHashSet();
            var missing = reference.Except(keys).ToList();
            var extra = keys.Except(reference).ToList();
            Assert.True(missing.Count == 0 && extra.Count == 0,
                $"{file}: missing [{string.Join(", ", missing.Take(5))}], extra [{string.Join(", ", extra.Take(5))}]");
        }
    }

    [Fact]
    public void Every_permission_code_has_localized_description()
    {
        foreach (var file in Files)
        {
            var keys = Load(file);
            var missing = AppPermissions.Catalog.Keys.Where(code => !keys.ContainsKey($"perm_{code}")).ToList();
            Assert.True(missing.Count == 0, $"{file}: perm_ keys missing for [{string.Join(", ", missing.Take(5))}]");
        }
    }

    /// Hisob tarixi operatsiya turini `op_<tur>` kaliti bilan chiqaradi; kalit bo'lmasa
    /// mijozga xom enum nomi ko'rinadi ("CustomerLoan"). Bu allaqachon bir marta yuz bergan.
    [Fact]
    public void Every_ledger_operation_has_a_localized_name()
    {
        foreach (var file in Files)
        {
            var keys = Load(file);
            var missing = Enum.GetNames<OperationType>()
                .Where(name => !keys.ContainsKey($"op_{name.ToLowerInvariant()}"))
                .ToList();
            Assert.True(missing.Count == 0, $"{file}: op_ keys missing for [{string.Join(", ", missing)}]");
        }
    }
}
