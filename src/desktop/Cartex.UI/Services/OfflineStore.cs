using SQLite;
using System.Text.Json;
using Cartex.Shared.Models.Sales;

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
    [Indexed] public string? CardBarcode { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DebtBalance { get; set; }
    public decimal CreditLimit { get; set; }
}

public class OfflineOutboxItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    [Indexed] public string EventId { get; set; } = "";
    [Indexed] public long LeaseId { get; set; }
    public long Epoch { get; set; }
    [Indexed] public long Sequence { get; set; }
    public long? ActorUserId { get; set; }
    public string PayloadJson { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime OccurredAt { get; set; }
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

    public async Task ReplaceSnapshotAsync(
        IEnumerable<OfflineProduct> products,
        IEnumerable<OfflineBarcode> barcodes,
        IEnumerable<OfflineCustomer> customers,
        long leaseId,
        long epoch,
        long snapshotVersion)
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

            // A wholesale server refresh must not erase stock effects of events
            // which are still only local. Reapply the current lease projection.
            var pending = c.Table<OfflineOutboxItem>()
                .Where(x => x.LeaseId == leaseId && x.Epoch == epoch
                    && (x.Status == "pending" || x.Status == "error"))
                .OrderBy(x => x.Sequence)
                .ToList();
            foreach (var item in pending.Where(x => x.Kind == "sale.create" || x.Kind == "sale"))
            {
                var sale = JsonSerializer.Deserialize<CreateSaleRequest>(item.PayloadJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (sale is null) continue;
                foreach (var line in sale.Items.GroupBy(x => x.VariantId)
                             .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) }))
                {
                    var product = c.Find<OfflineProduct>(line.VariantId);
                    if (product is null) continue;
                    product.Quantity -= line.Quantity;
                    c.Update(product);
                }
            }
            c.InsertOrReplace(new OfflineMeta { Key = "snapshot_version", Value = snapshotVersion.ToString() });
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

    public async Task PrepareLeaseAsync(OfflineLeaseCredential credential)
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            var sequence = credential.LastAcceptedSequence;
            var rows = c.Table<OfflineOutboxItem>()
                .Where(x => x.Status == "pending" || x.Status == "error")
                .OrderBy(x => x.Id)
                .ToList();
            foreach (var row in rows)
            {
                if (row.LeaseId == 0)
                {
                    row.LeaseId = credential.LeaseId;
                    row.Epoch = credential.Epoch;
                    row.EventId = string.IsNullOrWhiteSpace(row.EventId) ? Guid.NewGuid().ToString("D") : row.EventId;
                    row.Sequence = ++sequence;
                    row.Kind = NormalizeKind(row.Kind);
                    row.OccurredAt = row.OccurredAt == default
                        ? (row.CreatedAt == default ? DateTime.UtcNow : row.CreatedAt.ToUniversalTime())
                        : row.OccurredAt;
                    c.Update(row);
                }
                else if (row.LeaseId == credential.LeaseId && row.Epoch == credential.Epoch)
                {
                    sequence = Math.Max(sequence, row.Sequence);
                }
            }
            c.InsertOrReplace(new OfflineMeta { Key = "next_sequence", Value = (sequence + 1).ToString() });
        });
    }

    public async Task<OfflineOutboxItem> EnqueueSaleAndAdjustStockAsync(
        string payloadJson,
        string idempotencyKey,
        OfflineLeaseCredential credential,
        long actorUserId,
        bool allowInsufficientStock)
    {
        await InitAsync();
        var draft = JsonSerializer.Deserialize<CreateSaleRequest>(payloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Oflayn savdo ma'lumoti noto'g'ri.");
        OfflineOutboxItem? created = null;
        await _db.RunInTransactionAsync(c =>
        {
            var meta = c.Find<OfflineMeta>("next_sequence");
            var sequence = meta is not null && long.TryParse(meta.Value, out var value)
                ? value
                : credential.LastAcceptedSequence + 1;
            var totals = draft.Items.GroupBy(x => x.VariantId)
                .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) })
                .ToList();
            foreach (var line in totals)
            {
                var product = c.Find<OfflineProduct>(line.VariantId)
                    ?? throw new InvalidOperationException("Mahsulot oflayn keshda topilmadi.");
                if (!allowInsufficientStock && product.Quantity < line.Quantity)
                    throw new InvalidOperationException($"{product.ProductName}: oflayn qoldiq yetarli emas.");
            }

            created = new OfflineOutboxItem
            {
                Kind = "sale.create",
                Key = idempotencyKey,
                EventId = Guid.NewGuid().ToString("D"),
                LeaseId = credential.LeaseId,
                Epoch = credential.Epoch,
                Sequence = sequence,
                ActorUserId = actorUserId,
                PayloadJson = payloadJson,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                OccurredAt = DateTime.UtcNow
            };
            c.Insert(created);
            foreach (var line in totals)
            {
                var product = c.Find<OfflineProduct>(line.VariantId)!;
                product.Quantity -= line.Quantity;
                c.Update(product);
            }
            c.InsertOrReplace(new OfflineMeta { Key = "next_sequence", Value = (sequence + 1).ToString() });
        });
        return created!;
    }

    public async Task<List<OfflineOutboxItem>> GetOutboxAsync(
        long leaseId,
        long epoch,
        string? status = null,
        int limit = 100)
    {
        await InitAsync();
        var q = _db.Table<OfflineOutboxItem>();
        q = q.Where(x => x.LeaseId == leaseId && x.Epoch == epoch);
        if (status is not null) q = q.Where(o => o.Status == status);
        return await q.OrderBy(o => o.Sequence).Take(limit).ToListAsync();
    }

    public async Task<int> CountOutboxAsync(long leaseId, long epoch, string status)
    {
        await InitAsync();
        return await _db.Table<OfflineOutboxItem>()
            .Where(o => o.LeaseId == leaseId && o.Epoch == epoch && o.Status == status)
            .CountAsync();
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
    public async Task<bool> GetAllowInsufficientStockSalesAsync() =>
        await GetMetaAsync("allow_insufficient_stock_sales") == "1";

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

    private static string NormalizeKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sale" or "sale.create" => "sale.create",
        "repay" or "payment" or "customer.payment" => "customer.payment.create",
        var normalized => normalized
    };
}
