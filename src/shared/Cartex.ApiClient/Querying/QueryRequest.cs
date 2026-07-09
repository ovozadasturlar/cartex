using System.Globalization;

namespace Cartex.ApiClient.Querying;

public sealed class QueryRequest
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _filters = new(StringComparer.OrdinalIgnoreCase);

    public static QueryRequest Create() =>
        new QueryRequest().With("timeZone", TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalHours);

    public QueryRequest Search(string? term) =>
        string.IsNullOrWhiteSpace(term) ? this : With("search", term.Trim());

    public QueryRequest Page(int page, int pageSize) =>
        With("page", page).With("pageSize", pageSize);

    public QueryRequest Sort(string? column, bool descending = false) =>
        string.IsNullOrWhiteSpace(column) ? this : With("sortBy", column).With("descending", descending);

    public QueryRequest Filter(string field, params string[] values)
    {
        if (values.Length == 0) return this;
        if (!_filters.TryGetValue(field, out var list)) _filters[field] = list = [];
        list.AddRange(values);
        return this;
    }

    public QueryRequest FilterFrom(string field, DateTime? from) =>
        from is { } f ? Filter(field, $">={Format(f)}") : this;

    public QueryRequest FilterTo(string field, DateTime? to) =>
        to is { } t ? Filter(field, $"<{Format(t)}") : this;

    public QueryRequest FilterRange(string field, DateTime? from, DateTime? to) =>
        FilterFrom(field, from).FilterTo(field, to);

    public QueryRequest FilterIn(string field, IEnumerable<object> values) =>
        Filter(field, $"in:{string.Join(',', values.Select(Format))}");

    public QueryRequest FilterContains(string field, string value) =>
        Filter(field, $"contains:{value}");

    public QueryRequest With(string key, object? value)
    {
        if (value is not null)
            _values[key] = Format(value);
        return this;
    }

    public IDictionary<string, object> Build()
    {
        var query = new Dictionary<string, object>(_values, StringComparer.OrdinalIgnoreCase);
        foreach (var (field, values) in _filters)
            query[$"filters[{field}]"] = values;
        return query;
    }

    private static string Format(object value) => value switch
    {
        string s => s,
        DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}
