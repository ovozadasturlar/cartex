using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Cartex.Catalog.Tool.Normalization;
using Cartex.Catalog.Tool.Packaging;
using Cartex.Catalog.Tool.Supabase;

namespace Cartex.Catalog.Tool;

public abstract record CommandOptions;

public sealed record NormalizeOptions(string? File, Uri? Url, string? Server, string Types, string OutputDirectory) : CommandOptions;

public sealed record PackOptions(string Input, string OutputDirectory, string ShopType, string ShopTypes, int Version, string Key) : CommandOptions;

public sealed record PushOptions(string Input, string Project, bool DryRun) : CommandOptions;

public sealed record KeygenOptions(string OutputDirectory) : CommandOptions;

public static class CommandLine
{
    public const string Usage = """
        usage:
          Cartex.Catalog.Tool normalize (--file <path> | --url <url>) [--server <path>] [--types <path>] [--out <dir>]
          Cartex.Catalog.Tool pack --in <catalog.csv> --version <n> --key <pem> [--shop-type <code>] [--shop-types <path>] [--out <dir>]
          Cartex.Catalog.Tool push --in <catalog.csv> [--project <ref>] [--dry-run]
          Cartex.Catalog.Tool keygen [--out <dir>]

        push reads the service key from the SUPABASE_SERVICE_KEY environment variable.
        """;

    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CommandOptions? options, [NotNullWhen(false)] out string? error)
    {
        if (args.Count > 0)
        {
            if (args[0].Equals("normalize", StringComparison.OrdinalIgnoreCase))
                return Normalize(args, out options, out error);
            if (args[0].Equals("pack", StringComparison.OrdinalIgnoreCase))
                return Pack(args, out options, out error);
            if (args[0].Equals("push", StringComparison.OrdinalIgnoreCase))
                return Push(args, out options, out error);
            if (args[0].Equals("keygen", StringComparison.OrdinalIgnoreCase))
                return Keygen(args, out options, out error);
        }

        options = null;
        error = "the supported commands are 'normalize', 'pack', 'push' and 'keygen'";
        return false;
    }

    private static bool Normalize(IReadOnlyList<string> args, out CommandOptions? options, out string? error)
    {
        options = null;
        if (!TryReadOptions(args, ["--file", "--url", "--server", "--types", "--out"], out var values, out error))
            return false;

        values.TryGetValue("--file", out var file);
        values.TryGetValue("--url", out var url);

        if (file is null == url is null)
        {
            error = "exactly one of --file and --url is required";
            return false;
        }

        Uri? source = null;
        if (url is not null && !Uri.TryCreate(url, UriKind.Absolute, out source))
        {
            error = $"'{url}' is not an absolute url";
            return false;
        }

        options = new NormalizeOptions(file, source, values.GetValueOrDefault("--server"),
            values.GetValueOrDefault("--types", TypeVocabulary.DefaultPath), Output(values));
        return true;
    }

    private static bool Pack(IReadOnlyList<string> args, out CommandOptions? options, out string? error)
    {
        options = null;
        if (!TryReadOptions(args, ["--in", "--out", "--shop-type", "--shop-types", "--version", "--key"], out var values, out error))
            return false;

        if (!values.TryGetValue("--in", out var input))
        {
            error = "--in is required";
            return false;
        }

        if (!values.TryGetValue("--key", out var key))
        {
            error = "--key is required";
            return false;
        }

        if (!values.TryGetValue("--version", out var number) ||
            !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version == 0)
        {
            error = "--version is required and must be a positive whole number";
            return false;
        }

        var shopType = values.GetValueOrDefault("--shop-type", CatalogPack.AllShopTypes);
        if (shopType.Length == 0 || !shopType.All(symbol => char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol) || symbol == '-'))
        {
            error = $"'{shopType}' is not a shop type code; use lower-case letters, digits and '-'";
            return false;
        }

        options = new PackOptions(input, Output(values), shopType,
            values.GetValueOrDefault("--shop-types", ShopTypes.DefaultPath), version, key);
        return true;
    }

    private static bool Push(IReadOnlyList<string> args, out CommandOptions? options, out string? error)
    {
        options = null;
        if (!TryReadOptions(args, ["--in", "--project"], out var values, out error, "--dry-run"))
            return false;

        if (!values.TryGetValue("--in", out var input))
        {
            error = "--in is required";
            return false;
        }

        var project = values.GetValueOrDefault("--project", MasterCatalog.DefaultProject);
        if (project.Length == 0 || !project.All(symbol => char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol)))
        {
            error = $"'{project}' is not a Supabase project ref; use the lower-case ref from the project url";
            return false;
        }

        options = new PushOptions(input, project, values.ContainsKey("--dry-run"));
        return true;
    }

    private static bool Keygen(IReadOnlyList<string> args, out CommandOptions? options, out string? error)
    {
        options = null;
        if (!TryReadOptions(args, ["--out"], out var values, out error))
            return false;

        options = new KeygenOptions(Output(values));
        return true;
    }

    private static bool TryReadOptions(IReadOnlyList<string> args, IReadOnlyList<string> allowed,
        out Dictionary<string, string> values, out string? error, params string[] flags)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;

        for (var i = 1; i < args.Count; i++)
        {
            if (flags.Contains(args[i], StringComparer.Ordinal))
            {
                values[args[i]] = string.Empty;
                continue;
            }

            if (i + 1 >= args.Count)
            {
                error = $"option '{args[i]}' has no value";
                return false;
            }

            if (!allowed.Contains(args[i], StringComparer.Ordinal))
            {
                error = $"unknown option '{args[i]}'";
                return false;
            }

            values[args[i]] = args[i + 1];
            i++;
        }

        return true;
    }

    private static string Output(Dictionary<string, string> values) =>
        values.GetValueOrDefault("--out", Directory.GetCurrentDirectory());
}
