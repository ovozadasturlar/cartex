using SQLite;

namespace Cartex.UI.Services;

public class OfflineProduct
{
    [PrimaryKey] public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public bool AllowsAmountEntry { get; set; }
    public decimal QuantityStep { get; set; } = 1;
}

public class OfflineBarcode
{
    [PrimaryKey] public string Code { get; set; } = "";
    public long VariantId { get; set; }
    public decimal PackQty { get; set; }
}

public class OfflineCustomer
{
    [PrimaryKey] public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? CardBarcode { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DebtBalance { get; set; }
    public decimal CreditLimit { get; set; }
}

public class OfflineOutboxItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OfflineMeta
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class OfflineStore
{
    private readonly SQLiteAsyncConnection _db;
    private Task? _init;

    public OfflineStore()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
        Directory.CreateDirectory(dir);
        _db = new SQLiteAsyncConnection(Path.Combine(dir, "offline.db3"));
    }

    private Task InitAsync() => _init ??= CreateTablesAsync();

    private async Task CreateTablesAsync()
    {
        await _db.CreateTableAsync<OfflineProduct>();
        await _db.CreateTableAsync<OfflineBarcode>();
        await _db.CreateTableAsync<OfflineCustomer>();
        await _db.CreateTableAsync<OfflineOutboxItem>();
        await _db.CreateTableAsync<OfflineMeta>();
    }

    public async Task ReplaceSnapshotAsync(IEnumerable<OfflineProduct> products, IEnumerable<OfflineBarcode> barcodes, IEnumerable<OfflineCustomer> customers)
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<OfflineProduct>();
            c.InsertAll(products);
            c.DeleteAll<OfflineBarcode>();
            c.InsertAll(barcodes);
            c.DeleteAll<OfflineCustomer>();
            c.InsertAll(customers);
        });
    }

    public async Task<List<OfflineProduct>> SearchProductsAsync(string? query, int limit)
    {
        await InitAsync();
        var q = _db.Table<OfflineProduct>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(p => p.ProductName.ToLower().Contains(term));
        }
        return await q.OrderBy(p => p.ProductName).Take(limit).ToListAsync();
    }

    public async Task<OfflineProduct?> GetProductAsync(long variantId)
    {
        await InitAsync();
        return await _db.FindAsync<OfflineProduct>(variantId);
    }

    public async Task<OfflineBarcode?> GetBarcodeAsync(string code)
    {
        await InitAsync();
        return await _db.FindAsync<OfflineBarcode>(code);
    }

    public async Task<OfflineCustomer?> GetCustomerByCardAsync(string code)
    {
        await InitAsync();
        return await _db.Table<OfflineCustomer>().Where(c => c.CardBarcode == code).FirstOrDefaultAsync();
    }

    public async Task<List<OfflineCustomer>> SearchCustomersAsync(string? query, int limit)
    {
        await InitAsync();
        var q = _db.Table<OfflineCustomer>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            q = q.Where(c => c.FullName.ToLower().Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
        }
        return await q.OrderBy(c => c.FullName).Take(limit).ToListAsync();
    }

    public async Task AdjustStockAsync(long variantId, decimal delta)
    {
        await InitAsync();
        var row = await _db.FindAsync<OfflineProduct>(variantId);
        if (row is null) return;
        row.Quantity += delta;
        await _db.UpdateAsync(row);
    }

    public async Task EnqueueAsync(OfflineOutboxItem item)
    {
        await InitAsync();
        await _db.InsertAsync(item);
    }

    public async Task<List<OfflineOutboxItem>> GetOutboxAsync(string? status = null)
    {
        await InitAsync();
        var q = _db.Table<OfflineOutboxItem>();
        if (status is not null) q = q.Where(o => o.Status == status);
        return await q.OrderBy(o => o.Id).ToListAsync();
    }

    public async Task<int> CountOutboxAsync(string status)
    {
        await InitAsync();
        return await _db.Table<OfflineOutboxItem>().Where(o => o.Status == status).CountAsync();
    }

    public async Task UpdateOutboxAsync(OfflineOutboxItem item)
    {
        await InitAsync();
        await _db.UpdateAsync(item);
    }

    public async Task<string?> GetMetaAsync(string key)
    {
        await InitAsync();
        return (await _db.FindAsync<OfflineMeta>(key))?.Value;
    }

    public async Task SetMetaAsync(string key, string value)
    {
        await InitAsync();
        await _db.InsertOrReplaceAsync(new OfflineMeta { Key = key, Value = value });
    }

    public async Task<bool> GetAllowDebtSalesAsync() => await GetMetaAsync("allow_debt_sales") != "0";

    public async Task ClearAsync()
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<OfflineProduct>();
            c.DeleteAll<OfflineBarcode>();
            c.DeleteAll<OfflineCustomer>();
            c.DeleteAll<OfflineMeta>();
        });
    }
}
