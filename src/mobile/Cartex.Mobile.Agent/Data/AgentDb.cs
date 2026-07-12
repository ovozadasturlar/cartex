using Cartex.Mobile.Agent.Services;
using SQLite;
using Cartex.Mobile.Core;

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
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<LocalCustomer>();
            c.InsertAll(customers);
        });
    }

    public async Task ReplaceVanStockAsync(IEnumerable<LocalVanStock> stock)
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<LocalVanStock>();
            c.InsertAll(stock);
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
            .Select(UzText.Fold).Where(t => t.Length > 0).ToList();
        if (tokens.Count == 0) return all;
        return all.Where(c =>
        {
            var fields = new[] { UzText.Fold(c.FullName), UzText.Fold(c.Address ?? ""), UzText.Fold(c.Phone ?? "") };
            var digits = string.Concat((c.Phone ?? "").Where(char.IsDigit));
            return tokens.All(t => fields.Any(f => f.Contains(t)) || (t.All(char.IsDigit) && digits.Contains(t)));
        }).ToList();
    }

    public async Task<List<LocalVanStock>> SearchVanStockAsync(string? query)
    {
        await InitAsync();
        var q = _db.Table<LocalVanStock>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(s => s.ProductName.ToLower().Contains(term));
        }
        return await q.OrderBy(s => s.ProductName).ToListAsync();
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
}
