using System.Globalization;
using System.Text;
using Cartex.Application.Catalog;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Shared.Models.Catalog;
using Microsoft.Data.Sqlite;

namespace Cartex.Infrastructure.Catalog;

public sealed class FileCatalogSource(CatalogPackStore packs, ISettingsService settings) : ICatalogSource
{
    private const string ByBarcode = """
        select barcode, name, name_cyrl, manufacturer, category, model, unit, pack_qty, image_path
        from products where barcode = $barcode limit 1;
        """;

    private const string BySearch = """
        select p.barcode, p.name, p.name_cyrl, p.manufacturer, p.category, p.model, p.unit, p.pack_qty, p.image_path
        from products p
        where p.rowid in (select rowid from products_fts where products_fts match $query)
        order by p.name limit $limit;
        """;

    public CatalogSourceMode Mode => CatalogSourceMode.File;

    public async Task<CatalogProductDto?> ByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var path = await packs.VerifiedPathAsync(cancellationToken);
        if (path is null)
            return null;

        await using var connection = await OpenAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ByBarcode;
        command.Parameters.AddWithValue("$barcode", barcode);

        var images = await ImageBaseAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? await MapAsync(reader, images, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<CatalogProductDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var path = await packs.VerifiedPathAsync(cancellationToken);
        var folded = PackFold.Build(query);
        if (path is null || folded.Length < CatalogReference.MinQueryLength)
            return [];

        await using var connection = await OpenAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = BySearch;
        command.Parameters.AddWithValue("$query", '"' + folded.Replace("\"", "\"\"", StringComparison.Ordinal) + '"');
        command.Parameters.AddWithValue("$limit", limit);

        var images = await ImageBaseAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CatalogProductDto>(limit);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(await MapAsync(reader, images, cancellationToken));
        return items;
    }

    private static async Task<CatalogProductDto> MapAsync(SqliteDataReader reader, string images, CancellationToken cancellationToken)
    {
        var category = await Text(reader, 4, cancellationToken);
        var separator = category?.IndexOf(" / ", StringComparison.Ordinal) ?? -1;
        var image = await Text(reader, 8, cancellationToken);

        return new CatalogProductDto(
            reader.GetString(0),
            reader.GetString(1),
            await Text(reader, 2, cancellationToken),
            await Text(reader, 3, cancellationToken),
            separator < 0 ? category : category![..separator],
            separator < 0 ? null : category![(separator + 3)..],
            await Text(reader, 5, cancellationToken),
            await Text(reader, 6, cancellationToken),
            await reader.IsDBNullAsync(7, cancellationToken)
                ? null
                : (decimal)reader.GetDouble(7),
            image is null || images.Length == 0 ? null : images + image.TrimStart('/'));
    }

    private async Task<string> ImageBaseAsync(CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<ProductReferenceSettings>(SettingKeys.ProductReference, cancellationToken)
            ?? new ProductReferenceSettings();
        var root = config.ImageBaseUrl.Trim();
        return root.Length == 0 ? string.Empty : root.TrimEnd('/') + '/';
    }

    private static async Task<string?> Text(SqliteDataReader reader, int ordinal, CancellationToken cancellationToken) =>
        await reader.IsDBNullAsync(ordinal, cancellationToken) ? null : reader.GetString(ordinal);

    private static async Task<SqliteConnection> OpenAsync(string path, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(CatalogPackReader.ConnectionString(path));
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

internal static class CatalogPackReader
{
    public static string ConnectionString(string path) => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadOnly
    }.ToString();
}

internal static class PackFold
{
    private const string Apostrophes = "'ʻʼ’`";

    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['ғ'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "yo", ['ж'] = "j", ['з'] = "z", ['и'] = "i", ['й'] = "y",
        ['к'] = "k", ['қ'] = "q", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['ў'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "x", ['ҳ'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh",
        ['щ'] = "sh", ['ъ'] = "", ['ы'] = "i", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya"
    };

    public static string Build(string text)
    {
        var latin = new StringBuilder(text.Length);
        foreach (var symbol in text.ToLowerInvariant())
            latin.Append(Transliteration.TryGetValue(symbol, out var replacement) ? replacement : symbol.ToString());

        var folded = new StringBuilder(latin.Length);
        foreach (var symbol in latin.ToString().Normalize(NormalizationForm.FormD))
        {
            if (Apostrophes.Contains(symbol, StringComparison.Ordinal)
                || CharUnicodeInfo.GetUnicodeCategory(symbol) == UnicodeCategory.NonSpacingMark)
                continue;

            folded.Append(char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol) ? symbol : ' ');
        }

        return string.Join(' ', folded.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
