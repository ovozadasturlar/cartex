using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Cartex.Catalog.Tool.Packaging;

public static class PackBuilder
{
    public const int SchemaVersion = 2;

    private const int PageSize = 4096;

    private const string Tables = """
        create table products (
            barcode      text not null,
            name         text not null,
            name_cyrl    text,
            search_fold  text not null,
            manufacturer text,
            category     text,
            model        text,
            unit         text not null,
            pack_qty     real,
            image_path   text
        );
        create table meta (key text primary key, value text not null);
        """;

    private const string Indexes = """
        create index products_barcode on products (barcode);
        create index products_fold on products (search_fold);
        """;

    private const string Search = """
        create virtual table products_fts using fts5 (
            name, name_cyrl, search_fold,
            content = 'products', content_rowid = 'rowid', tokenize = 'trigram');
        insert into products_fts (rowid, name, name_cyrl, search_fold)
            select rowid, name, name_cyrl, search_fold from products order by rowid;
        insert into products_fts (products_fts) values ('optimize');
        """;

    private const string Insert = """
        insert into products values ($barcode, $name, $name_cyrl, $search_fold,
            $manufacturer, $category, $model, $unit, $pack_qty, $image_path);
        """;

    public static async Task Build(string path, string shopType, int version, IReadOnlyList<PackRow> rows)
    {
        File.Delete(path);

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());

        await connection.OpenAsync();
        await Execute(connection, $"pragma page_size = {PageSize};");
        await Execute(connection, Tables);
        await WriteProducts(connection, rows);
        await Execute(connection, Indexes);
        await Execute(connection, Search);
        await WriteMeta(connection, shopType, version, rows.Count);
        await Execute(connection, "vacuum;");
    }

    private static async Task WriteProducts(SqliteConnection connection, IReadOnlyList<PackRow> rows)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Insert;

        var barcode = command.Parameters.Add("$barcode", SqliteType.Text);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        var nameCyrillic = command.Parameters.Add("$name_cyrl", SqliteType.Text);
        var searchFold = command.Parameters.Add("$search_fold", SqliteType.Text);
        var manufacturer = command.Parameters.Add("$manufacturer", SqliteType.Text);
        var category = command.Parameters.Add("$category", SqliteType.Text);
        var model = command.Parameters.Add("$model", SqliteType.Text);
        var unit = command.Parameters.Add("$unit", SqliteType.Text);
        var packQuantity = command.Parameters.Add("$pack_qty", SqliteType.Real);
        var imagePath = command.Parameters.Add("$image_path", SqliteType.Text);

        foreach (var row in rows)
        {
            barcode.Value = row.Barcode;
            name.Value = row.Name;
            nameCyrillic.Value = row.NameCyrillic;
            searchFold.Value = row.SearchFold;
            manufacturer.Value = Optional(row.Manufacturer);
            category.Value = Optional(row.Category);
            model.Value = Optional(row.Model);
            unit.Value = row.Unit;
            packQuantity.Value = Optional(row.PackQuantity);
            imagePath.Value = Optional(row.ImagePath);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task WriteMeta(SqliteConnection connection, string shopType, int version, int rowCount)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "insert into meta values ($key, $value);";
        var key = command.Parameters.Add("$key", SqliteType.Text);
        var value = command.Parameters.Add("$value", SqliteType.Text);

        foreach (var (name, content) in Meta(shopType, version, rowCount))
        {
            key.Value = name;
            value.Value = content;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static (string Key, string Value)[] Meta(string shopType, int version, int rowCount) =>
    [
        ("rowCount", rowCount.ToString(CultureInfo.InvariantCulture)),
        ("schemaVersion", SchemaVersion.ToString(CultureInfo.InvariantCulture)),
        ("shopType", shopType),
        ("version", version.ToString(CultureInfo.InvariantCulture))
    ];

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static object Optional(object? value) => value ?? DBNull.Value;
}
