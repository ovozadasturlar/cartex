using System.Security.Cryptography;
using System.Text.Json;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using SQLite;

namespace Cartex.Mobile.Store.Services;

public sealed class MobileOfflineProduct
{
    [PrimaryKey] public long VariantId { get; set; }
    [Indexed] public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string UnitName { get; set; } = "";
    public string Dimension { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal SellingPrice { get; set; }
    public bool AllowsAmountEntry { get; set; }
    public bool AllowsFractional { get; set; }
}

public sealed class MobileOfflineBarcode
{
    [PrimaryKey] public string Code { get; set; } = "";
    [Indexed] public long VariantId { get; set; }
    public decimal PackQty { get; set; }
}

public sealed class MobileOfflineCustomer
{
    [PrimaryKey] public long Id { get; set; }
    [Indexed] public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    [Indexed] public string? CardBarcode { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal DebtBalance { get; set; }
    public decimal CreditLimit { get; set; }
}

public sealed class MobileOfflineParticipantRole
{
    [PrimaryKey] public long Id { get; set; }
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool CanEqualBuyer { get; set; }
    public int MaxCount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class MobileOfflinePartner
{
    [PrimaryKey] public long PartnerId { get; set; }
    [Indexed] public long PartyId { get; set; }
    public string PartnerCode { get; set; } = "";
    [Indexed] public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    [Indexed] public long? CustomerId { get; set; }
}

public sealed class MobileOfflineOutbox
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public string EventId { get; set; } = "";
    [Indexed] public long LeaseId { get; set; }
    public long Epoch { get; set; }
    [Indexed] public long Sequence { get; set; }
    public long ActorUserId { get; set; }
    public string Kind { get; set; } = "sale.create";
    public string IdempotencyKey { get; set; } = "";
    // Payload is encrypted at rest; product/customer projections contain no bearer credentials.
    public string PayloadCipher { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public DateTime OccurredAt { get; set; }
}

public sealed class MobileOfflineMeta
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class MobileOfflineStore
{
    private const string PayloadKeyName = "offline_payload_key_v2";
    private readonly SQLiteAsyncConnection _db = new(Path.Combine(
        FileSystem.AppDataDirectory, "offline-store-v2.db3"));
    private Task? _init;
    private byte[] _key = [];

    public Task InitializeAsync() => _init ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        var encoded = await SecureStorage.GetAsync(PayloadKeyName);
        if (string.IsNullOrWhiteSpace(encoded))
        {
            _key = RandomNumberGenerator.GetBytes(32);
            await SecureStorage.SetAsync(PayloadKeyName, Convert.ToBase64String(_key));
        }
        else
        {
            _key = Convert.FromBase64String(encoded);
        }

        await _db.CreateTableAsync<MobileOfflineProduct>();
        await _db.CreateTableAsync<MobileOfflineBarcode>();
        await _db.CreateTableAsync<MobileOfflineCustomer>();
        await _db.CreateTableAsync<MobileOfflineParticipantRole>();
        await _db.CreateTableAsync<MobileOfflinePartner>();
        await _db.CreateTableAsync<MobileOfflineOutbox>();
        await _db.CreateTableAsync<MobileOfflineMeta>();
    }

    public async Task ReplaceSnapshotAsync(OfflineSnapshotDto snapshot)
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<MobileOfflineProduct>();
            c.InsertAll(snapshot.Products.Select(x => new MobileOfflineProduct
            {
                VariantId = x.VariantId,
                ProductName = x.ProductName,
                CategoryName = x.CategoryName,
                UnitName = x.UnitName,
                Dimension = x.Dimension,
                Quantity = x.Quantity,
                SellingPrice = x.SellingPrice,
                AllowsAmountEntry = x.AllowsAmountEntry,
                AllowsFractional = x.AllowsFractional
            }));
            c.DeleteAll<MobileOfflineBarcode>();
            c.InsertAll(snapshot.Barcodes.Select(x => new MobileOfflineBarcode
            {
                Code = x.Code,
                VariantId = x.VariantId,
                PackQty = x.PackQty
            }));
            c.DeleteAll<MobileOfflineCustomer>();
            c.InsertAll(snapshot.Customers.Select(x => new MobileOfflineCustomer
            {
                Id = x.Id,
                FullName = x.FullName,
                Phone = x.Phone,
                CardBarcode = x.CardBarcode,
                DiscountPct = x.DiscountPct,
                DebtBalance = x.DebtBalance,
                CreditLimit = x.CreditLimit
            }));
            c.DeleteAll<MobileOfflineParticipantRole>();
            c.InsertAll((snapshot.ParticipantRoles ?? []).Select(x => new MobileOfflineParticipantRole
            {
                Id = x.Id,
                Key = x.Key,
                Label = x.SingularLabel,
                IsRequired = x.IsRequired,
                CanEqualBuyer = x.CanEqualBuyer,
                MaxCount = x.MaxCount,
                SortOrder = x.SortOrder
            }));
            c.DeleteAll<MobileOfflinePartner>();
            c.InsertAll((snapshot.Partners ?? []).Select(x => new MobileOfflinePartner
            {
                PartnerId = x.PartnerId,
                PartyId = x.PartyId,
                PartnerCode = x.PartnerCode,
                FullName = x.FullName,
                Phone = x.Phone,
                CustomerId = x.CustomerId
            }));

            var pending = c.Table<MobileOfflineOutbox>()
                .Where(x => x.LeaseId == snapshot.LeaseId && x.Epoch == snapshot.Epoch
                    && (x.Status == "pending" || x.Status == "error"))
                .OrderBy(x => x.Sequence)
                .ToList();
            foreach (var item in pending.Where(x => x.Kind == "sale.create"))
                ReapplySale(c, Decrypt(item.PayloadCipher));

            Put(c, "base_currency", snapshot.BaseCurrency);
            Put(c, "allow_debt_sales", snapshot.AllowDebtSales ? "1" : "0");
            Put(c, "allow_insufficient_stock_sales", snapshot.AllowInsufficientStockSales ? "1" : "0");
            Put(c, "snapshot_version", snapshot.SnapshotVersion.ToString());
            Put(c, "last_sync", snapshot.ServerTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
        });
    }

    public async Task PrepareLeaseAsync(MobileOfflineCredential credential)
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(c =>
        {
            var next = credential.LastAcceptedSequence + 1;
            var legacy = c.Table<MobileOfflineOutbox>()
                .Where(x => x.LeaseId == 0 && (x.Status == "pending" || x.Status == "error"))
                .OrderBy(x => x.Id).ToList();
            foreach (var row in legacy)
            {
                row.LeaseId = credential.LeaseId;
                row.Epoch = credential.Epoch;
                row.Sequence = next++;
                row.EventId = string.IsNullOrWhiteSpace(row.EventId) ? Guid.NewGuid().ToString("D") : row.EventId;
                c.Update(row);
            }
            var max = c.Table<MobileOfflineOutbox>()
                .Where(x => x.LeaseId == credential.LeaseId && x.Epoch == credential.Epoch)
                .OrderByDescending(x => x.Sequence)
                .FirstOrDefault()?.Sequence ?? 0;
            Put(c, "next_sequence", Math.Max(next, max + 1).ToString());
        });
    }

    public async Task EnqueueSaleAsync(
        CreateSaleRequest sale,
        MobileOfflineCredential credential,
        long actorUserId)
    {
        await InitializeAsync();
        var idempotencyKey = string.IsNullOrWhiteSpace(sale.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : sale.IdempotencyKey;
        sale = sale with { IdempotencyKey = idempotencyKey, ApplyAutoDiscount = false, UseCustomerAdvance = false };
        var json = JsonSerializer.Serialize(sale, JsonOptions);
        var cipher = Encrypt(json);
        await _db.RunInTransactionAsync(c =>
        {
            var nextRow = c.Find<MobileOfflineMeta>("next_sequence");
            var next = nextRow is not null && long.TryParse(nextRow.Value, out var parsed)
                ? parsed
                : credential.LastAcceptedSequence + 1;
            var grouped = sale.Items.GroupBy(x => x.VariantId)
                .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) })
                .ToList();
            var allowInsufficient = c.Find<MobileOfflineMeta>("allow_insufficient_stock_sales")?.Value == "1";
            foreach (var line in grouped)
            {
                var product = c.Find<MobileOfflineProduct>(line.VariantId)
                    ?? throw new InvalidOperationException("Mahsulot oflayn keshda topilmadi.");
                if (!allowInsufficient && product.Quantity < line.Quantity)
                    throw new InvalidOperationException($"{product.ProductName}: oflayn qoldiq yetarli emas.");
            }
            c.Insert(new MobileOfflineOutbox
            {
                EventId = Guid.NewGuid().ToString("D"),
                LeaseId = credential.LeaseId,
                Epoch = credential.Epoch,
                Sequence = next,
                ActorUserId = actorUserId,
                Kind = "sale.create",
                IdempotencyKey = idempotencyKey!,
                PayloadCipher = cipher,
                OccurredAt = DateTime.UtcNow
            });
            foreach (var line in grouped)
            {
                var product = c.Find<MobileOfflineProduct>(line.VariantId)!;
                product.Quantity -= line.Quantity;
                c.Update(product);
            }
            Put(c, "next_sequence", (next + 1).ToString());
        });
    }

    public async Task<(MobileOfflineProduct Product, decimal PackQty)?> GetByBarcodeAsync(string code)
    {
        await InitializeAsync();
        var barcode = await _db.FindAsync<MobileOfflineBarcode>(code);
        if (barcode is null) return null;
        var product = await _db.FindAsync<MobileOfflineProduct>(barcode.VariantId);
        return product is null ? null : (product, barcode.PackQty);
    }

    public async Task<MobileOfflineProduct?> GetProductAsync(long variantId)
    {
        await InitializeAsync();
        return await _db.FindAsync<MobileOfflineProduct>(variantId);
    }

    public async Task<List<MobileOfflineProduct>> SearchProductsAsync(string term, int limit)
    {
        await InitializeAsync();
        var normalized = term.Trim().ToLowerInvariant();
        return await _db.Table<MobileOfflineProduct>()
            .Where(x => x.ProductName.ToLower().Contains(normalized))
            .OrderBy(x => x.ProductName).Take(limit).ToListAsync();
    }

    public async Task<List<MobileOfflineCustomer>> SearchCustomersAsync(string term, int limit)
    {
        await InitializeAsync();
        var normalized = term.Trim().ToLowerInvariant();
        return await _db.Table<MobileOfflineCustomer>()
            .Where(x => x.FullName.ToLower().Contains(normalized)
                || (x.Phone != null && x.Phone.Contains(normalized)))
            .OrderBy(x => x.FullName).Take(limit).ToListAsync();
    }

    public async Task<MobileOfflineCustomer?> GetCustomerAsync(long customerId)
    {
        await InitializeAsync();
        return await _db.FindAsync<MobileOfflineCustomer>(customerId);
    }

    public async Task<List<MobileOfflineParticipantRole>> GetRolesAsync()
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflineParticipantRole>().OrderBy(x => x.SortOrder).ToListAsync();
    }

    public async Task<List<MobileOfflinePartner>> SearchPartnersAsync(string term, int limit)
    {
        await InitializeAsync();
        var normalized = term.Trim().ToLowerInvariant();
        return await _db.Table<MobileOfflinePartner>()
            .Where(x => x.FullName.ToLower().Contains(normalized)
                || (x.Phone != null && x.Phone.Contains(normalized)))
            .OrderBy(x => x.FullName).Take(limit).ToListAsync();
    }

    public async Task<MobileOfflinePartner?> FindPartnerByCustomerAsync(long customerId)
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflinePartner>()
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);
    }

    public async Task<List<MobileOfflineOutbox>> GetPendingAsync(long leaseId, long epoch, int limit)
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflineOutbox>()
            .Where(x => x.LeaseId == leaseId && x.Epoch == epoch && x.Status == "pending")
            .OrderBy(x => x.Sequence).Take(limit).ToListAsync();
    }

    public async Task<int> CountAsync(long leaseId, long epoch, string status)
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflineOutbox>()
            .CountAsync(x => x.LeaseId == leaseId && x.Epoch == epoch && x.Status == status);
    }

    public async Task UpdateAsync(MobileOfflineOutbox row)
    {
        await InitializeAsync();
        await _db.UpdateAsync(row);
    }

    public string ReadPayload(MobileOfflineOutbox row) => Decrypt(row.PayloadCipher);

    public async Task<string?> GetMetaAsync(string key)
    {
        await InitializeAsync();
        return (await _db.FindAsync<MobileOfflineMeta>(key))?.Value;
    }

    public async Task ClearProjectionAsync()
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<MobileOfflineProduct>();
            c.DeleteAll<MobileOfflineBarcode>();
            c.DeleteAll<MobileOfflineCustomer>();
            c.DeleteAll<MobileOfflineParticipantRole>();
            c.DeleteAll<MobileOfflinePartner>();
            c.DeleteAll<MobileOfflineMeta>();
        });
    }

    private void ReapplySale(SQLiteConnection connection, string json)
    {
        var sale = JsonSerializer.Deserialize<CreateSaleRequest>(json, JsonOptions);
        if (sale is null) return;
        foreach (var line in sale.Items.GroupBy(x => x.VariantId)
                     .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) }))
        {
            var product = connection.Find<MobileOfflineProduct>(line.VariantId);
            if (product is null) continue;
            product.Quantity -= line.Quantity;
            connection.Update(product);
        }
    }

    private string Encrypt(string value)
    {
        var plain = System.Text.Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    private string Decrypt(string value)
    {
        var payload = Convert.FromBase64String(value);
        if (payload.Length < 28) throw new CryptographicException("Oflayn payload buzilgan.");
        var plain = new byte[payload.Length - 28];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(payload.AsSpan(0, 12), payload.AsSpan(28), payload.AsSpan(12, 16), plain);
        return System.Text.Encoding.UTF8.GetString(plain);
    }

    private static void Put(SQLiteConnection connection, string key, string value) =>
        connection.InsertOrReplace(new MobileOfflineMeta { Key = key, Value = value });

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
