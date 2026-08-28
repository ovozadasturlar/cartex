using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Cartex.Catalog.Tool.Io;
using Cartex.Catalog.Tool.Normalization;
using Cartex.Catalog.Tool.Packaging;
using Cartex.Catalog.Tool.Supabase;

namespace Cartex.Catalog.Tool;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!CommandLine.TryParse(args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync(error);
            await Console.Error.WriteLineAsync(CommandLine.Usage);
            return 1;
        }

        try
        {
            return options switch
            {
                NormalizeOptions normalize => await Normalize(normalize),
                PackOptions pack => await Pack(pack),
                PushOptions push => await Push(push),
                KeygenOptions keygen => await Keygen(keygen),
                _ => throw new UnreachableException()
            };
        }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException
                                            or HttpRequestException or CryptographicException or DbException)
        {
            await Console.Error.WriteLineAsync(failure.Message);
            return 1;
        }
    }

    private static async Task<int> Normalize(NormalizeOptions options)
    {
        var types = TypeVocabulary.Load(options.Types);
        var workbook = options.Url is null
            ? await File.ReadAllBytesAsync(options.File!)
            : await Download(options.Url);

        using var content = new MemoryStream(workbook);
        var rows = new List<SourceRow>(WorkbookReader.Read(content));
        if (options.Server is not null)
            rows.AddRange(ServerCatalogReader.Read(options.Server));

        var report = CatalogNormalizer.Normalize(rows, types);

        Directory.CreateDirectory(options.OutputDirectory);
        var catalogPath = Path.Combine(options.OutputDirectory, "catalog.csv");
        var issuesPath = Path.Combine(options.OutputDirectory, "issues.csv");
        CsvFile.Write(catalogPath, CatalogNormalizer.ProductHeader, report.Products.Select(CatalogNormalizer.ToFields));
        CsvFile.Write(issuesPath, CatalogNormalizer.IssueHeader, report.Issues.Select(CatalogNormalizer.ToFields));

        Summarize(report, catalogPath, issuesPath);
        return 0;
    }

    private static async Task<int> Pack(PackOptions options)
    {
        using var key = PackSigner.Load(options.Key);
        var shopType = options.ShopType == CatalogPack.AllShopTypes ? null : ShopTypes.Load(options.ShopTypes, options.ShopType);
        var rows = CatalogPack.Read(options.Input, shopType);

        Directory.CreateDirectory(options.OutputDirectory);
        var stem = Path.Combine(options.OutputDirectory, $"catalog-{options.ShopType}-v{options.Version}");
        var packPath = stem + ".db";
        var manifestPath = stem + ".json";

        await PackBuilder.Build(packPath, options.ShopType, options.Version, rows);

        var hash = PackSigner.Hash(packPath);
        var size = new FileInfo(packPath).Length;
        var manifest = PackManifest.Create(options.ShopType, options.Version, rows.Count, size, hash, key.SignHash(hash));
        JsonFile.Write(manifestPath, manifest);

        Console.WriteLine(Line("shop type", options.ShopType));
        Console.WriteLine(Line("version", options.Version));
        Console.WriteLine(Line("rows", rows.Count));
        Console.WriteLine(Line("bytes", manifest.ByteSize.ToString(CultureInfo.InvariantCulture)));
        Console.WriteLine(Line("sha256", manifest.Sha256));

        Console.WriteLine();
        Console.WriteLine($"wrote {packPath}");
        Console.WriteLine($"wrote {manifestPath}");
        return 0;
    }

    private static async Task<int> Push(PushOptions options)
    {
        using var catalog = MasterCatalog.Open(options.Project);
        var report = await CatalogPush.Run(catalog, options.Input, options.DryRun, Console.WriteLine);

        Console.WriteLine();
        Console.WriteLine(Line("products", report.Products));
        Console.WriteLine(Line("  new", report.Created));
        Console.WriteLine(Line("  changed", report.Changed));
        Console.WriteLine(Line("  unchanged", report.Unchanged));
        Console.WriteLine(Line("manufacturers", report.Manufacturers));
        Console.WriteLine(Line("  new", report.NewManufacturers));
        Console.WriteLine(Line("categories", report.CategoriesResolved));
        Console.WriteLine(Line("  not found", report.CategoriesMissing));
        Console.WriteLine(Line("only in master", report.OnlyInMaster));

        Console.WriteLine();
        Console.WriteLine(options.DryRun
            ? "dry run: nothing was written"
            : $"pushed to '{options.Project}'");
        return 0;
    }

    private static async Task<int> Keygen(KeygenOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var privatePath = Path.Combine(options.OutputDirectory, PackSigner.PrivateKeyFile);
        var publicPath = Path.Combine(options.OutputDirectory, PackSigner.PublicKeyFile);
        if (File.Exists(privatePath))
            throw new IOException($"'{privatePath}' already exists; refusing to overwrite a signing key.");

        var keys = PackSigner.Create();
        await File.WriteAllTextAsync(privatePath, keys.PrivatePem);
        await File.WriteAllTextAsync(publicPath, keys.PublicPem);

        Console.WriteLine($"wrote {privatePath}");
        Console.WriteLine($"wrote {publicPath}");
        Console.WriteLine("keep the private key out of the repository and off shared storage");
        return 0;
    }

    private static async Task<byte[]> Download(Uri url)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(url);
    }

    private static void Summarize(CatalogReport report, string catalogPath, string issuesPath)
    {
        Console.WriteLine(Line("rows read", report.RowsRead));
        Console.WriteLine(Line("accepted", report.Products.Count));
        foreach (var group in report.Products.GroupBy(product => product.Source, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            Console.WriteLine(Line("  " + group.Key, group.Count()));

        Console.WriteLine(Line("rejected", report.RowsRead - report.Products.Count));
        Console.WriteLine(Line("manufacturers", report.Manufacturers.Count));

        Console.WriteLine();
        Console.WriteLine("issues");
        foreach (var group in report.Issues.GroupBy(issue => issue.Code, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            Console.WriteLine(Line("  " + group.Key, group.Count()));

        Console.WriteLine();
        Console.WriteLine("manufacturer near-duplicates (reported, never merged)");
        if (report.ManufacturerConflicts.Count == 0)
            Console.WriteLine("  none");
        foreach (var conflict in report.ManufacturerConflicts)
            Console.WriteLine($"  {conflict.First} ~ {conflict.Second} ({conflict.Reason})");

        Console.WriteLine();
        Console.WriteLine($"wrote {catalogPath}");
        Console.WriteLine($"wrote {issuesPath}");
    }

    private static string Line(string label, int value) =>
        Line(label, value.ToString(CultureInfo.InvariantCulture));

    private static string Line(string label, string value) =>
        $"{label,-16}{value,8}";
}
