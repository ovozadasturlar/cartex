using SQLite;
using SearchFolding = Cartex.Shared.Search.SearchFold;

namespace Cartex.Mobile.Agent.Data;

public sealed class AgentDb
{
    private readonly SQLiteAsyncConnection _db = new(Path.Combine(FileSystem.AppDataDirectory, "agent.db3"));
    private Task? _init;

    private Task InitAsync() => _init ??= CreateTablesAsync();

    private async Task CreateTablesAsync()
    {
        await _db.CreateTableAsync<LocalCustomer>();
        await _db.CreateTableAsync<LocalVanStock>();
        await _db.CreateTableAsync<OutboxItem>();
        await _db.CreateTableAsync<MetaEntry>();
        await _db.CreateTableAsync<LocalOrder>();
    }

    public async Task ReplaceCustomersAsync(IEnumerable<LocalCustomer> customers)
    {
        await InitAsync();
        var rows = customers.ToList();
        foreach (var row in rows)
            row.SearchFold = SearchFolding.Fuzzy(row.FullName);
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<LocalCustomer>();
            c.InsertAll(rows);
        });
    }

    public async Task ReplaceVanStockAsync(IEnumerable<LocalVanStock> stock)
    {
        await InitAsync();
        var rows = stock.ToList();
        foreach (var row in rows)
            row.SearchFold = SearchFolding.Fuzzy(row.ProductName);
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<LocalVanStock>();
            c.InsertAll(rows);
        });
    }

    public async Task<List<LocalCustomer>> GetCustomersAsync()
    {
        await InitAsync();
        return await _db.Table<LocalCustomer>().OrderBy(c => c.FullName).ToListAsync();
    }

    public async Task<List<LocalVanStock>> GetVanStockAsync()
    {
        await InitAsync();
        return await _db.Table<LocalVanStock>().OrderBy(s => s.ProductName).ToListAsync();
    }

    public async Task<int> CountAsync<T>() where T : new()
    {
        await InitAsync();
        return await _db.Table<T>().CountAsync();
    }

    public async Task<string?> GetMetaAsync(string key)
    {
        await InitAsync();
        var row = await _db.FindAsync<MetaEntry>(key);
        return row?.Value;
    }

    public async Task SetMetaAsync(string key, string value)
    {
        await InitAsync();
        await _db.InsertOrReplaceAsync(new MetaEntry { Key = key, Value = value });
    }

    public async Task<LocalCustomer?> GetCustomerAsync(long id)
    {
        await InitAsync();
        return await _db.FindAsync<LocalCustomer>(id);
    }

    public async Task<List<LocalCustomer>> SearchCustomersAsync(string? query)
    {
        await InitAsync();
        var all = await _db.Table<LocalCustomer>().OrderBy(c => c.FullName).ToListAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(SearchFolding.Fuzzy).Where(t => t.Length > 0).ToList();
        if (tokens.Count == 0) return all;
        var folded = SearchFolding.Fuzzy(query);
        var strict = SearchFolding.Strict(query);
        return all.Where(c => (folded.Length > 0
                && c.SearchFold?.Contains(folded, StringComparison.Ordinal) == true)
            || CustomerFieldsMatch(c, tokens))
            .OrderBy(c => NameRank(c.FullName, strict))
            .ThenBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .ToList();
    }

    public async Task<List<LocalVanStock>> SearchVanStockAsync(string? query)
    {
        await InitAsync();
        var all = await _db.Table<LocalVanStock>().ToListAsync();
        return FilterVanStock(all, query);
    }

    public static List<LocalVanStock> FilterVanStock(IEnumerable<LocalVanStock> stock, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return stock.OrderBy(s => s.ProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.VariantId).ToList();
        var term = query.Trim();
        var folded = SearchFolding.Fuzzy(query);
        var strict = SearchFolding.Strict(query);
        return stock.Where(s => s.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (folded.Length > 0
                    && s.SearchFold?.Contains(folded, StringComparison.Ordinal) == true)
                || s.Code?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(s => NameRank(s.ProductName, strict))
            .ThenBy(s => s.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.VariantId)
            .ToList();
    }

    public async Task<LocalVanStock?> FindByBarcodeAsync(string code)
    {
        await InitAsync();
        var term = $"|{code.Trim()}|";
        return await _db.Table<LocalVanStock>().Where(s => s.Barcodes.Contains(term)).FirstOrDefaultAsync()
            ?? await _db.Table<LocalVanStock>().Where(s => s.Code == code).FirstOrDefaultAsync();
    }

    public async Task AdjustStockAsync(long variantId, decimal delta)
    {
        await InitAsync();
        var row = await _db.FindAsync<LocalVanStock>(variantId);
        if (row is null) return;
        row.Quantity += delta;
        await _db.UpdateAsync(row);
    }

    public async Task AdjustDebtAsync(long customerId, decimal delta)
    {
        await InitAsync();
        var row = await _db.FindAsync<LocalCustomer>(customerId);
        if (row is null) return;
        row.DebtBalance += delta;
        await _db.UpdateAsync(row);
    }

    public async Task EnqueueAsync(OutboxItem item)
    {
        await InitAsync();
        await _db.InsertAsync(item);
    }

    public async Task<List<OutboxItem>> GetOutboxAsync()
    {
        await InitAsync();
        return await _db.Table<OutboxItem>().OrderByDescending(o => o.Id).ToListAsync();
    }

    public async Task<List<OutboxItem>> GetPendingOutboxAsync()
    {
        await InitAsync();
        return await _db.Table<OutboxItem>().Where(o => o.Status == "pending").OrderBy(o => o.Id).ToListAsync();
    }

    public async Task<int> CountOutboxAsync(string status)
    {
        await InitAsync();
        return await _db.Table<OutboxItem>().Where(o => o.Status == status).CountAsync();
    }

    public async Task UpdateOutboxAsync(OutboxItem item)
    {
        await InitAsync();
        await _db.UpdateAsync(item);
    }

    public async Task DeleteOutboxAsync(int id)
    {
        await InitAsync();
        await _db.DeleteAsync<OutboxItem>(id);
    }

    public async Task ClearCacheAsync()
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<LocalCustomer>();
            c.DeleteAll<LocalVanStock>();
            c.DeleteAll<MetaEntry>();
            c.DeleteAll<OutboxItem>();
            c.DeleteAll<LocalOrder>();
        });
    }

    public async Task SaveOrderAsync(LocalOrder order)
    {
        await InitAsync();
        await _db.InsertOrReplaceAsync(order);
    }

    public async Task<List<LocalOrder>> GetOrdersAsync()
    {
        await InitAsync();
        return await _db.Table<LocalOrder>().OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<LocalOrder?> GetOrderAsync(string localId)
    {
        await InitAsync();
        return await _db.FindAsync<LocalOrder>(localId);
    }

    public async Task SetOrderCodeAsync(string localId, string code)
    {
        await InitAsync();
        var row = await _db.FindAsync<LocalOrder>(localId);
        if (row is null) return;
        row.Code = code;
        if (row.Status == "new") row.Status = "synced";
        await _db.UpdateAsync(row);
    }

    public async Task SetOrderStatusByCodeAsync(string code, string status)
    {
        await InitAsync();
        var row = await _db.Table<LocalOrder>().Where(o => o.Code == code).FirstOrDefaultAsync();
        if (row is null) return;
        row.Status = status;
        if (status == "delivered") row.DeliveredAt ??= DateTime.Now;
        await _db.UpdateAsync(row);
    }

    public async Task DeleteOrderAsync(string localId)
    {
        await InitAsync();
        await _db.DeleteAsync<LocalOrder>(localId);
    }

    private static bool CustomerFieldsMatch(LocalCustomer customer, IReadOnlyList<string> tokens)
    {
        var fields = new[]
        {
            SearchFolding.Fuzzy(customer.FullName),
            SearchFolding.Fuzzy(customer.Address),
            SearchFolding.Fuzzy(customer.Phone)
        };
        var digits = string.Concat((customer.Phone ?? "").Where(char.IsDigit));
        return tokens.All(token => fields.Any(field => field.Contains(token, StringComparison.Ordinal))
            || (token.All(char.IsDigit) && digits.Contains(token, StringComparison.Ordinal)));
    }

    private static int NameRank(string name, string strictQuery)
    {
        var strictName = SearchFolding.Strict(name);
        if (strictName.StartsWith(strictQuery, StringComparison.Ordinal)) return 0;
        return strictName.Contains(strictQuery, StringComparison.Ordinal) ? 1 : 2;
    }
}
