using SQLite;
using System.Text.Json;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Supplies;
using SearchFolding = Cartex.Shared.Search.SearchFold;

namespace Cartex.UI.Services;

public class OfflineProduct
{
    [PrimaryKey] public long VariantId { get; set; }
    public string ProductName { get; set; } = "";
    [Indexed] public string? SearchFold { get; set; }
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public bool AllowsAmountEntry { get; set; }
    public bool AllowsFractional { get; set; }
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
    [Indexed] public string? SearchFold { get; set; }
    public string? Phone { get; set; }
    [Indexed] public string? CardBarcode { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DebtBalance { get; set; }
    public decimal? CreditLimit { get; set; }
}

public class OfflineSupplier
{
    [PrimaryKey] public long Id { get; set; }
    public string Name { get; set; } = "";
    [Indexed] public string? SearchFold { get; set; }
    public string? Phone { get; set; }
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
    public DateTime? PushedAt { get; set; }
}

public class OfflineMeta
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed record OfflineSnapshotRemovals(
    IReadOnlyList<long> ProductIds,
    IReadOnlyList<string> BarcodeCodes,
    IReadOnlyList<long> CustomerIds,
    IReadOnlyList<long> SupplierIds);

public sealed class OfflineStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SQLiteAsyncConnection _db;
    private Task? _init;

    /// Yo'l testlar uchun ochiq: navbat, dedup va qoldiq proyeksiyasi — pul tegadigan yo'l,
    /// uni haqiqiy bazaga qarshi tekshirib bo'lmasa, xato faqat kassada bilinardi.
    public OfflineStore(string? databasePath = null)
    {
        if (databasePath is null)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(dir);
            databasePath = Path.Combine(dir, "offline.db3");
        }
        _db = new SQLiteAsyncConnection(databasePath);
    }

    private Task InitAsync() => _init ??= CreateTablesAsync();

    private async Task CreateTablesAsync()
    {
        await _db.CreateTableAsync<OfflineProduct>();
        await _db.CreateTableAsync<OfflineBarcode>();
        await _db.CreateTableAsync<OfflineCustomer>();
        await _db.CreateTableAsync<OfflineSupplier>();
        await _db.CreateTableAsync<OfflineOutboxItem>();
        await _db.CreateTableAsync<OfflineMeta>();
        await _db.RunInTransactionAsync(c =>
        {
            var products = c.Table<OfflineProduct>().Where(x => x.SearchFold == null).ToList();
            var customers = c.Table<OfflineCustomer>().Where(x => x.SearchFold == null).ToList();
            var suppliers = c.Table<OfflineSupplier>().Where(x => x.SearchFold == null).ToList();
            FillSearchFolds(products, customers, suppliers);
            foreach (var product in products) c.Update(product);
            foreach (var customer in customers) c.Update(customer);
            foreach (var supplier in suppliers) c.Update(supplier);
        });
    }

    public async Task ReplaceSnapshotAsync(
        IEnumerable<OfflineProduct> products,
        IEnumerable<OfflineBarcode> barcodes,
        IEnumerable<OfflineCustomer> customers,
        IEnumerable<OfflineSupplier> suppliers,
        long leaseId,
        long epoch,
        long snapshotVersion)
    {
        await InitAsync();
        var productRows = products.ToList();
        var customerRows = customers.ToList();
        var supplierRows = suppliers.ToList();
        FillSearchFolds(productRows, customerRows, supplierRows);
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<OfflineProduct>();
            c.InsertAll(productRows);
            c.DeleteAll<OfflineBarcode>();
            c.InsertAll(barcodes);
            c.DeleteAll<OfflineCustomer>();
            c.InsertAll(customerRows);
            c.DeleteAll<OfflineSupplier>();
            c.InsertAll(supplierRows);

            // A wholesale server refresh must not erase local effects of events
            // which are still only local. Reapply the current lease projection.
            foreach (var item in PendingProjections(c, leaseId, epoch))
                ApplyProjection(c, item, 1);
            c.InsertOrReplace(new OfflineMeta { Key = "snapshot_version", Value = snapshotVersion.ToString() });
        });
    }

    /// OFF-53: delta faqat serverdan kelgan qatorlarni almashtiradi va `Removed*` dagilarini
    /// o'chiradi; ko'rsatilmagan qator hech qachon o'chirilmaydi.
    public async Task ApplyDeltaAsync(
        IReadOnlyCollection<OfflineProduct> products,
        IReadOnlyCollection<OfflineBarcode> barcodes,
        IReadOnlyCollection<OfflineCustomer> customers,
        IReadOnlyCollection<OfflineSupplier> suppliers,
        OfflineSnapshotRemovals removed,
        long leaseId,
        long epoch,
        long snapshotVersion)
    {
        await InitAsync();
        FillSearchFolds(products, customers, suppliers);
        await _db.RunInTransactionAsync(c =>
        {
            foreach (var id in removed.ProductIds) c.Delete<OfflineProduct>(id);
            foreach (var code in removed.BarcodeCodes) c.Delete<OfflineBarcode>(code);
            foreach (var id in removed.CustomerIds) c.Delete<OfflineCustomer>(id);
            foreach (var id in removed.SupplierIds) c.Delete<OfflineSupplier>(id);
            foreach (var row in products) c.InsertOrReplace(row);
            foreach (var row in barcodes) c.InsertOrReplace(row);
            foreach (var row in customers) c.InsertOrReplace(row);
            foreach (var row in suppliers) c.InsertOrReplace(row);

            // Serverdan kelgan qatorda hali yuborilmagan mahalliy amallar ko'rinmaydi. Ular
            // faqat yangilangan kalitlar uchun qayta qo'llanadi: filtrsiz qo'llash tegilmagan
            // qatorlarga ikkinchi marta tushib, qoldiqni ikki barobar kamaytirardi.
            var variantIds = products.Select(x => x.VariantId).ToHashSet();
            var customerIds = customers.Select(x => x.Id).ToHashSet();
            if (variantIds.Count > 0 || customerIds.Count > 0)
                foreach (var item in PendingProjections(c, leaseId, epoch))
                    ApplyProjection(c, item, 1, variantIds, customerIds);
            c.InsertOrReplace(new OfflineMeta { Key = "snapshot_version", Value = snapshotVersion.ToString() });
        });
    }

    /// HUB-09: yo'ldoshga beriladigan katalog shu keshdan olinadi — HUB o'zidan ma'lumot qo'shmaydi.
    public async Task<(List<OfflineProduct> Products, List<OfflineBarcode> Barcodes,
        List<OfflineCustomer> Customers, List<OfflineSupplier> Suppliers)> GetSnapshotAsync()
    {
        await InitAsync();
        return (await _db.Table<OfflineProduct>().ToListAsync(),
            await _db.Table<OfflineBarcode>().ToListAsync(),
            await _db.Table<OfflineCustomer>().ToListAsync(),
            await _db.Table<OfflineSupplier>().ToListAsync());
    }

    public async Task<(int Products, int Barcodes, int Customers, int Suppliers)> CountSnapshotAsync()
    {
        await InitAsync();
        return (await _db.Table<OfflineProduct>().CountAsync(),
            await _db.Table<OfflineBarcode>().CountAsync(),
            await _db.Table<OfflineCustomer>().CountAsync(),
            await _db.Table<OfflineSupplier>().CountAsync());
    }

    private static List<OfflineOutboxItem> PendingProjections(SQLiteConnection c, long leaseId, long epoch) =>
        c.Table<OfflineOutboxItem>()
            .Where(x => x.LeaseId == leaseId && x.Epoch == epoch
                && (x.Status == "pending" || x.Status == "error"))
            .OrderBy(x => x.Sequence)
            .ToList();

    private static void ApplyProjection(
        SQLiteConnection c,
        OfflineOutboxItem item,
        int sign,
        HashSet<long>? variantIds = null,
        HashSet<long>? customerIds = null)
    {
        switch (NormalizeKind(item.Kind))
        {
            case "sale.create":
            {
                var sale = JsonSerializer.Deserialize<CreateSaleRequest>(item.PayloadJson, Json);
                if (sale is null) return;
                foreach (var line in sale.Items.GroupBy(x => x.VariantId))
                {
                    if (variantIds?.Contains(line.Key) == false) continue;
                    var product = c.Find<OfflineProduct>(line.Key);
                    if (product is null) continue;
                    product.Quantity -= sign * line.Sum(x => x.Quantity);
                    c.Update(product);
                }
                return;
            }
            case "customer.payment.create":
            {
                var payment = JsonSerializer.Deserialize<CreateCustomerPaymentRequest>(item.PayloadJson, Json);
                if (payment is null) return;
                if (customerIds?.Contains(payment.CustomerId) == false) return;
                var customer = c.Find<OfflineCustomer>(payment.CustomerId);
                if (customer is null) return;
                customer.DebtBalance -= sign * payment.Tenders.Sum(x => x.Amount);
                c.Update(customer);
                return;
            }
            case "supply.create":
            {
                var supply = JsonSerializer.Deserialize<CreateSupplyRequest>(item.PayloadJson, Json);
                if (supply is null) return;
                foreach (var line in supply.Items.Where(x => x.UnitId is null && x.PackId is null))
                {
                    if (variantIds?.Contains(line.VariantId) == false) continue;
                    var product = c.Find<OfflineProduct>(line.VariantId);
                    if (product is null) continue;
                    product.Quantity += sign * line.Quantity;
                    if (sign > 0 && line.SellingPrice is { } price)
                        product.SellingPrice = price;
                    c.Update(product);
                }
                return;
            }
        }
    }

    public async Task<List<OfflineProduct>> SearchProductsAsync(string? query, int limit)
    {
        await InitAsync();
        var q = _db.Table<OfflineProduct>();
        string? strict = null;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            var folded = SearchFolding.Fuzzy(query);
            strict = SearchFolding.Strict(query);
            q = q.Where(p => p.ProductName.ToLower().Contains(term)
                || (folded.Length > 0 && p.SearchFold != null && p.SearchFold.Contains(folded)));
        }
        var rows = await q.ToListAsync();
        return rows
            .OrderBy(p => strict is null ? 0 : NameRank(p.ProductName, strict))
            .ThenBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.VariantId)
            .Take(limit)
            .ToList();
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
        string? strict = null;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            var folded = SearchFolding.Fuzzy(query);
            strict = SearchFolding.Strict(query);
            q = q.Where(c => c.FullName.ToLower().Contains(term)
                || (folded.Length > 0 && c.SearchFold != null && c.SearchFold.Contains(folded))
                || (c.Phone != null && c.Phone.Contains(term)));
        }
        var rows = await q.ToListAsync();
        return rows
            .OrderBy(c => strict is null ? 0 : NameRank(c.FullName, strict))
            .ThenBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .Take(limit)
            .ToList();
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

    /// HUB-08: yo'ldosh buferi vakolatga bog'langan. Vakolat boshqa qurilmaga ko'chsa eski
    /// `(leaseId, epoch)` qatorlari hech bir so'rovga tushmay qolardi — pul jimgina yo'qolardi.
    /// Yuborilmagan qatorlar yangi vakolatga ko'chiriladi va uning ketma-ketligini oladi;
    /// `EventId` o'zgarmaydi, shuning uchun takror yuborish `HUB-06`/`OFF-01` dedupida yutiladi.
    public async Task AdoptOutboxAsync(OfflineLeaseCredential credential)
    {
        await InitAsync();
        await _db.RunInTransactionAsync(c =>
        {
            var rows = c.Table<OfflineOutboxItem>()
                .Where(x => (x.Status == "pending" || x.Status == "error")
                    && (x.LeaseId != credential.LeaseId || x.Epoch != credential.Epoch))
                .OrderBy(x => x.Id)
                .ToList();
            if (rows.Count == 0) return;

            var meta = c.Find<OfflineMeta>("next_sequence");
            var sequence = meta is not null && long.TryParse(meta.Value, out var value)
                ? value
                : credential.LastAcceptedSequence + 1;
            foreach (var row in rows)
            {
                row.LeaseId = credential.LeaseId;
                row.Epoch = credential.Epoch;
                row.Sequence = sequence++;
                c.Update(row);
            }
            c.InsertOrReplace(new OfflineMeta { Key = "next_sequence", Value = sequence.ToString() });
        });
    }

    public async Task<OfflineOutboxItem> EnqueueSaleAndAdjustStockAsync(
        string payloadJson,
        string idempotencyKey,
        OfflineLeaseCredential credential,
        long actorUserId,
        bool allowInsufficientStock)
    {
        return await EnqueueAsync("sale.create", payloadJson, idempotencyKey, credential, actorUserId,
            SaleValidator(payloadJson, allowInsufficientStock));
    }

    private static Action<SQLiteConnection> SaleValidator(string payloadJson, bool allowInsufficientStock)
    {
        var draft = JsonSerializer.Deserialize<CreateSaleRequest>(payloadJson, Json)
            ?? throw new InvalidOperationException("Oflayn savdo ma'lumoti noto'g'ri.");
        return c =>
        {
            foreach (var line in draft.Items.GroupBy(x => x.VariantId)
                         .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) }))
            {
                var product = c.Find<OfflineProduct>(line.VariantId)
                    ?? throw new InvalidOperationException("Mahsulot oflayn keshda topilmadi.");
                if (!allowInsufficientStock && product.Quantity < line.Quantity)
                    throw new InvalidOperationException($"{product.ProductName}: oflayn qoldiq yetarli emas.");
            }
        };
    }

    public async Task<OfflineOutboxItem> EnqueuePaymentAndAdjustDebtAsync(
        string payloadJson,
        string idempotencyKey,
        OfflineLeaseCredential credential,
        long actorUserId)
    {
        return await EnqueueAsync("customer.payment.create", payloadJson, idempotencyKey, credential, actorUserId,
            PaymentValidator(payloadJson));
    }

    private static Action<SQLiteConnection> PaymentValidator(string payloadJson)
    {
        var draft = JsonSerializer.Deserialize<CreateCustomerPaymentRequest>(payloadJson, Json)
            ?? throw new InvalidOperationException("Oflayn to'lov ma'lumoti noto'g'ri.");
        return c =>
        {
            if (c.Find<OfflineCustomer>(draft.CustomerId) is null)
                throw new InvalidOperationException("Mijoz oflayn keshda topilmadi.");
        };
    }

    public async Task<OfflineOutboxItem> EnqueueSupplyAndAdjustStockAsync(
        string payloadJson,
        string idempotencyKey,
        OfflineLeaseCredential credential,
        long actorUserId)
    {
        return await EnqueueAsync("supply.create", payloadJson, idempotencyKey, credential, actorUserId,
            SupplyValidator(payloadJson));
    }

    private static Action<SQLiteConnection> SupplyValidator(string payloadJson)
    {
        _ = JsonSerializer.Deserialize<CreateSupplyRequest>(payloadJson, Json)
            ?? throw new InvalidOperationException("Oflayn kirim ma'lumoti noto'g'ri.");
        return _ => { };
    }

    /// HUB-06: yo'ldoshdan kelgan hodisa HUB navbatiga **o'z `EventId`si bilan** yoziladi va
    /// HUB ketma-ketligini oladi. Takror yuborilsa mavjud qator qaytariladi — hujjat ikkilanmaydi.
    public async Task<OfflineOutboxItem> EnqueueFromSatelliteAsync(
        string kind,
        string payloadJson,
        string idempotencyKey,
        Guid eventId,
        DateTime occurredAt,
        OfflineLeaseCredential credential,
        long actorUserId,
        bool allowInsufficientStock)
    {
        var validate = kind switch
        {
            "sale.create" => SaleValidator(payloadJson, allowInsufficientStock),
            "customer.payment.create" => PaymentValidator(payloadJson),
            "supply.create" => SupplyValidator(payloadJson),
            _ => throw new InvalidOperationException("Bu amal turi HUB orqali qabul qilinmaydi.")
        };
        return await EnqueueAsync(kind, payloadJson, idempotencyKey, credential, actorUserId,
            validate, eventId, occurredAt);
    }

    private async Task<OfflineOutboxItem> EnqueueAsync(
        string kind,
        string payloadJson,
        string idempotencyKey,
        OfflineLeaseCredential credential,
        long actorUserId,
        Action<SQLiteConnection>? validate = null,
        Guid? eventId = null,
        DateTime? occurredAt = null)
    {
        await InitAsync();
        OfflineOutboxItem? created = null;
        await _db.RunInTransactionAsync(c =>
        {
            if (eventId is { } id)
            {
                var key = id.ToString("D");
                var existing = c.Table<OfflineOutboxItem>().FirstOrDefault(x => x.EventId == key);
                if (existing is not null)
                {
                    created = existing;
                    return;
                }
            }

            validate?.Invoke(c);
            var meta = c.Find<OfflineMeta>("next_sequence");
            var sequence = meta is not null && long.TryParse(meta.Value, out var value)
                ? value
                : credential.LastAcceptedSequence + 1;
            created = new OfflineOutboxItem
            {
                Kind = kind,
                Key = idempotencyKey,
                EventId = (eventId ?? Guid.NewGuid()).ToString("D"),
                LeaseId = credential.LeaseId,
                Epoch = credential.Epoch,
                Sequence = sequence,
                ActorUserId = actorUserId,
                PayloadJson = payloadJson,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                OccurredAt = occurredAt ?? DateTime.UtcNow
            };
            c.Insert(created);
            ApplyProjection(c, created, 1);
            c.InsertOrReplace(new OfflineMeta { Key = "next_sequence", Value = (sequence + 1).ToString() });
        });
        return created!;
    }

    public async Task<bool> CancelPendingAsync(int outboxId)
    {
        await InitAsync();
        var cancelled = false;
        await _db.RunInTransactionAsync(c =>
        {
            var item = c.Find<OfflineOutboxItem>(outboxId);
            if (item is null || item.Status != "pending" || item.PushedAt is not null) return;
            ApplyProjection(c, item, -1);
            c.Delete(item);
            var later = c.Table<OfflineOutboxItem>()
                .Where(x => x.LeaseId == item.LeaseId && x.Epoch == item.Epoch && x.Sequence > item.Sequence)
                .OrderBy(x => x.Sequence)
                .ToList();
            foreach (var row in later)
            {
                row.Sequence--;
                c.Update(row);
            }
            var meta = c.Find<OfflineMeta>("next_sequence");
            if (meta is not null && long.TryParse(meta.Value, out var next) && next > item.Sequence)
                c.InsertOrReplace(new OfflineMeta { Key = "next_sequence", Value = (next - 1).ToString() });
            cancelled = true;
        });
        return cancelled;
    }

    public async Task MarkPushedAsync(IReadOnlyCollection<int> ids)
    {
        await InitAsync();
        var now = DateTime.UtcNow;
        await _db.RunInTransactionAsync(c =>
        {
            foreach (var id in ids)
            {
                var item = c.Find<OfflineOutboxItem>(id);
                if (item is null || item.PushedAt is not null) continue;
                item.PushedAt = now;
                c.Update(item);
            }
        });
    }

    public async Task<List<OfflineSupplier>> GetSuppliersAsync()
    {
        await InitAsync();
        return await _db.Table<OfflineSupplier>().OrderBy(s => s.Name).ToListAsync();
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

    /// Joriy vakolatga tegishli bo'lmagan yuborilmagan qatorlar. Ular hech bir yuborishga
    /// tushmaydi, shuning uchun hech bo'lmaganda ro'yxatda va sanoqda ko'rinishi shart.
    public async Task<List<OfflineOutboxItem>> GetOrphanOutboxAsync(long leaseId, long epoch, int limit)
    {
        await InitAsync();
        return await OrphanQuery(leaseId, epoch).OrderBy(o => o.Id).Take(limit).ToListAsync();
    }

    public async Task<int> CountOrphanOutboxAsync(long leaseId, long epoch)
    {
        await InitAsync();
        return await OrphanQuery(leaseId, epoch).CountAsync();
    }

    private AsyncTableQuery<OfflineOutboxItem> OrphanQuery(long leaseId, long epoch) =>
        _db.Table<OfflineOutboxItem>()
            .Where(o => (o.Status == "pending" || o.Status == "error")
                && (o.LeaseId != leaseId || o.Epoch != epoch));

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

    public async Task RemoveMetaAsync(string key)
    {
        await InitAsync();
        await _db.DeleteAsync<OfflineMeta>(key);
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
            c.DeleteAll<OfflineSupplier>();
            c.DeleteAll<OfflineMeta>();
        });
    }

    private static string NormalizeKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "sale" or "sale.create" => "sale.create",
        "repay" or "payment" or "customer.payment" => "customer.payment.create",
        "supply" => "supply.create",
        var normalized => normalized
    };

    private static void FillSearchFolds(
        IEnumerable<OfflineProduct> products,
        IEnumerable<OfflineCustomer> customers,
        IEnumerable<OfflineSupplier> suppliers)
    {
        foreach (var product in products)
            product.SearchFold = SearchFolding.Fuzzy(product.ProductName);
        foreach (var customer in customers)
            customer.SearchFold = SearchFolding.Fuzzy(customer.FullName);
        foreach (var supplier in suppliers)
            supplier.SearchFold = SearchFolding.Fuzzy(supplier.Name);
    }

    private static int NameRank(string name, string strictQuery)
    {
        var strictName = SearchFolding.Strict(name);
        if (strictName.StartsWith(strictQuery, StringComparison.Ordinal))
            return 0;
        return strictName.Contains(strictQuery, StringComparison.Ordinal) ? 1 : 2;
    }
}
