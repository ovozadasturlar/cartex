using System.Security.Cryptography;
using System.Text.Json;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Supplies;
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
    public decimal? CreditLimit { get; set; }
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

public sealed class MobileOfflineSupplier
{
    [PrimaryKey] public long Id { get; set; }
    [Indexed] public string Name { get; set; } = "";
    public string? Phone { get; set; }
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
    public DateTime? PushedAt { get; set; }
}

public sealed class MobileOfflineMeta
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class MobileOfflineStore
{
    // HUB-07: yo'ldosh navbati HUB'ning lizingiga emas, shu doimiy kalitga yoziladi. Aks holda
    // vakolat boshqa qurilmaga ko'chganda (yangi lease/epoch) yuborilmagan qatorlar hech qaysi
    // so'rovga tushmay qolardi — pul jimgina yo'qolardi.
    public const long SatelliteLeaseId = -1;
    public const long SatelliteEpoch = 0;

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
        await _db.CreateTableAsync<MobileOfflineSupplier>();
        await _db.CreateTableAsync<MobileOfflineOutbox>();
        await _db.CreateTableAsync<MobileOfflineMeta>();
    }

    // `owner` — navbat kimning nomiga yozilgani. Snapshot'dagi lizing bilan bir xil emas: HUB
    // katalogi o'z lizingini olib keladi, yuborilmagan qatorlar esa yo'ldosh chelagida turadi.
    public async Task ReplaceSnapshotAsync(OfflineSnapshotDto snapshot, MobileOfflineCredential owner)
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(c =>
        {
            c.DeleteAll<MobileOfflineProduct>();
            c.InsertAll(snapshot.Products.Select(Product));
            c.DeleteAll<MobileOfflineBarcode>();
            c.InsertAll(snapshot.Barcodes.Select(Barcode));
            c.DeleteAll<MobileOfflineCustomer>();
            c.InsertAll(snapshot.Customers.Select(Customer));
            c.DeleteAll<MobileOfflineSupplier>();
            c.InsertAll((snapshot.Suppliers ?? []).Select(Supplier));
            ReplaceParties(c, snapshot);

            foreach (var item in PendingProjections(c, owner.LeaseId, owner.Epoch))
                ApplyProjection(c, item.Kind, Decrypt(item.PayloadCipher), 1);
            PutSnapshotMeta(c, snapshot);
        });
    }

    // OFF-53: delta faqat kelgan qatorlarni almashtiradi va `Removed*` dagilarini o'chiradi.
    // Rol/hamkor ro'yxatlari serverdan har doim to'liq keladi, shuning uchun ular almashtiriladi.
    public async Task ApplyDeltaAsync(OfflineSnapshotDto snapshot, MobileOfflineCredential owner)
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(c =>
        {
            foreach (var id in snapshot.RemovedProductIds) c.Delete<MobileOfflineProduct>(id);
            foreach (var code in snapshot.RemovedBarcodeCodes) c.Delete<MobileOfflineBarcode>(code);
            foreach (var id in snapshot.RemovedCustomerIds) c.Delete<MobileOfflineCustomer>(id);
            foreach (var id in snapshot.RemovedSupplierIds) c.Delete<MobileOfflineSupplier>(id);
            foreach (var row in snapshot.Products) c.InsertOrReplace(Product(row));
            foreach (var row in snapshot.Barcodes) c.InsertOrReplace(Barcode(row));
            foreach (var row in snapshot.Customers) c.InsertOrReplace(Customer(row));
            foreach (var row in snapshot.Suppliers ?? []) c.InsertOrReplace(Supplier(row));
            ReplaceParties(c, snapshot);

            // Serverdan kelgan qatorda hali yuborilmagan mahalliy amallar ko'rinmaydi. Ular
            // faqat yangilangan kalitlar uchun qayta qo'llanadi: filtrsiz qo'llash tegilmagan
            // qatorlarga ikkinchi marta tushib, qoldiqni ikki barobar kamaytirardi.
            var variantIds = snapshot.Products.Select(x => x.VariantId).ToHashSet();
            var customerIds = snapshot.Customers.Select(x => x.Id).ToHashSet();
            if (variantIds.Count > 0 || customerIds.Count > 0)
                foreach (var item in PendingProjections(c, owner.LeaseId, owner.Epoch))
                    ApplyProjection(c, item.Kind, Decrypt(item.PayloadCipher), 1, variantIds, customerIds);
            PutSnapshotMeta(c, snapshot);
        });
    }

    // HUB-09: yo'ldoshga beriladigan katalog shu keshdan olinadi.
    public async Task<(List<MobileOfflineProduct> Products, List<MobileOfflineBarcode> Barcodes,
        List<MobileOfflineCustomer> Customers, List<MobileOfflineSupplier> Suppliers)> GetSnapshotAsync()
    {
        await InitializeAsync();
        return (await _db.Table<MobileOfflineProduct>().ToListAsync(),
            await _db.Table<MobileOfflineBarcode>().ToListAsync(),
            await _db.Table<MobileOfflineCustomer>().ToListAsync(),
            await _db.Table<MobileOfflineSupplier>().ToListAsync());
    }

    public async Task<(int Products, int Barcodes, int Customers, int Suppliers)> CountSnapshotAsync()
    {
        await InitializeAsync();
        return (await _db.Table<MobileOfflineProduct>().CountAsync(),
            await _db.Table<MobileOfflineBarcode>().CountAsync(),
            await _db.Table<MobileOfflineCustomer>().CountAsync(),
            await _db.Table<MobileOfflineSupplier>().CountAsync());
    }

    private static MobileOfflineProduct Product(Cartex.Shared.Models.Stocks.StockOnHandDto x) => new()
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
    };

    private static MobileOfflineBarcode Barcode(OfflineBarcodeDto x) => new()
    {
        Code = x.Code,
        VariantId = x.VariantId,
        PackQty = x.PackQty
    };

    private static MobileOfflineCustomer Customer(OfflineCustomerDto x) => new()
    {
        Id = x.Id,
        FullName = x.FullName,
        Phone = x.Phone,
        CardBarcode = x.CardBarcode,
        DiscountPct = x.DiscountPct,
        DebtBalance = x.DebtBalance,
        CreditLimit = x.CreditLimit
    };

    private static MobileOfflineSupplier Supplier(OfflineSupplierDto x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Phone = x.Phone
    };

    private static void ReplaceParties(SQLiteConnection c, OfflineSnapshotDto snapshot)
    {
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
    }

    private static List<MobileOfflineOutbox> PendingProjections(SQLiteConnection c, long leaseId, long epoch) =>
        c.Table<MobileOfflineOutbox>()
            .Where(x => x.LeaseId == leaseId && x.Epoch == epoch
                && (x.Status == "pending" || x.Status == "error"))
            .OrderBy(x => x.Sequence)
            .ToList();

    private static void PutSnapshotMeta(SQLiteConnection c, OfflineSnapshotDto snapshot)
    {
        Put(c, "base_currency", snapshot.BaseCurrency);
        Put(c, "allow_debt_sales", snapshot.AllowDebtSales ? "1" : "0");
        Put(c, "allow_insufficient_stock_sales", snapshot.AllowInsufficientStockSales ? "1" : "0");
        Put(c, "snapshot_version", snapshot.SnapshotVersion.ToString());
        Put(c, "last_sync", snapshot.ServerTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
    }

    public async Task PrepareLeaseAsync(MobileOfflineCredential credential)
    {
        await InitializeAsync();
        // Qurilma o'z vakolatini olganda yo'ldosh chelagida qolgan yuborilmagan qatorlar yangi
        // lizingga ko'chiriladi — aks holda ular faqat HUB orqali ketadigan bo'lib, HUB
        // ko'tarilmasa navbatda abadiy qolardi. Server EventId bo'yicha dedup qiladi: HUB
        // ularni allaqachon olgan bo'lsa ikkinchi marta qo'llanmaydi.
        var adopt = credential.LeaseId == SatelliteLeaseId ? 0 : SatelliteLeaseId;
        await _db.RunInTransactionAsync(c =>
        {
            // Ko'chirilgan qator lizingdagi mavjud qatorlardan keyin raqamlanadi: bir xil
            // sequence ikki qatorda bo'lsa serverda butun zanjir `sequence_conflict` bilan to'xtardi.
            var max = c.Table<MobileOfflineOutbox>()
                .Where(x => x.LeaseId == credential.LeaseId && x.Epoch == credential.Epoch)
                .OrderByDescending(x => x.Sequence)
                .FirstOrDefault()?.Sequence ?? 0;
            var next = Math.Max(credential.LastAcceptedSequence + 1, max + 1);
            var orphans = c.Table<MobileOfflineOutbox>()
                .Where(x => (x.LeaseId == 0 || x.LeaseId == adopt)
                    && (x.Status == "pending" || x.Status == "error"))
                .OrderBy(x => x.Id).ToList();
            foreach (var row in orphans)
            {
                row.LeaseId = credential.LeaseId;
                row.Epoch = credential.Epoch;
                row.Sequence = next++;
                row.EventId = string.IsNullOrWhiteSpace(row.EventId) ? Guid.NewGuid().ToString("D") : row.EventId;
                c.Update(row);
            }
            Put(c, "next_sequence", next.ToString());
        });
    }

    public async Task EnqueueSaleAsync(
        CreateSaleRequest sale,
        MobileOfflineCredential credential,
        long actorUserId)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(sale.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : sale.IdempotencyKey;
        sale = sale with { IdempotencyKey = idempotencyKey, ApplyAutoDiscount = false, UseCustomerAdvance = false };
        await EnqueueAsync("sale.create", JsonSerializer.Serialize(sale, JsonOptions),
            idempotencyKey!, credential, actorUserId, c =>
        {
            var allowInsufficient = c.Find<MobileOfflineMeta>("allow_insufficient_stock_sales")?.Value == "1";
            foreach (var line in sale.Items.GroupBy(x => x.VariantId)
                         .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) }))
            {
                var product = c.Find<MobileOfflineProduct>(line.VariantId)
                    ?? throw new InvalidOperationException("Mahsulot oflayn keshda topilmadi.");
                if (!allowInsufficient && product.Quantity < line.Quantity)
                    throw new InvalidOperationException($"{product.ProductName}: oflayn qoldiq yetarli emas.");
            }
        });
    }

    public async Task EnqueuePaymentAsync(
        CreateCustomerPaymentRequest payment,
        MobileOfflineCredential credential,
        long actorUserId) =>
        await EnqueueAsync("customer.payment.create", JsonSerializer.Serialize(payment, JsonOptions),
            payment.IdempotencyKey!, credential, actorUserId, c =>
        {
            if (c.Find<MobileOfflineCustomer>(payment.CustomerId) is null)
                throw new InvalidOperationException("Mijoz oflayn keshda topilmadi.");
        });

    public async Task EnqueueSupplyAsync(
        CreateSupplyRequest supply,
        MobileOfflineCredential credential,
        long actorUserId) =>
        await EnqueueAsync("supply.create", JsonSerializer.Serialize(supply, JsonOptions),
            supply.IdempotencyKey!, credential, actorUserId);

    // HUB-06: yo'ldoshdan kelgan hodisa o'z EventId'si bilan yoziladi va HUB ketma-ketligini
    // oladi; takror yuborilsa mavjud qatorning ketma-ketligi qaytariladi, ikkinchi qator yaratilmaydi.
    public async Task<long> EnqueueFromSatelliteAsync(
        string kind,
        string json,
        string idempotencyKey,
        Guid eventId,
        DateTime occurredAt,
        MobileOfflineCredential credential,
        long actorUserId)
    {
        Action<SQLiteConnection> validate = kind switch
        {
            "sale.create" => SaleValidator(json),
            "customer.payment.create" => PaymentValidator(json),
            "supply.create" => _ => { },
            _ => throw new InvalidOperationException("Bu amal turi HUB orqali qabul qilinmaydi.")
        };
        return await EnqueueAsync(kind, json, idempotencyKey, credential, actorUserId,
            validate, eventId, occurredAt);
    }

    private static Action<SQLiteConnection> SaleValidator(string json)
    {
        var sale = JsonSerializer.Deserialize<CreateSaleRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Oflayn savdo ma'lumoti noto'g'ri.");
        return c =>
        {
            var allowInsufficient = c.Find<MobileOfflineMeta>("allow_insufficient_stock_sales")?.Value == "1";
            foreach (var line in sale.Items.GroupBy(x => x.VariantId)
                         .Select(x => new { VariantId = x.Key, Quantity = x.Sum(y => y.Quantity) }))
            {
                var product = c.Find<MobileOfflineProduct>(line.VariantId)
                    ?? throw new InvalidOperationException("Mahsulot oflayn keshda topilmadi.");
                if (!allowInsufficient && product.Quantity < line.Quantity)
                    throw new InvalidOperationException($"{product.ProductName}: oflayn qoldiq yetarli emas.");
            }
        };
    }

    private static Action<SQLiteConnection> PaymentValidator(string json)
    {
        var payment = JsonSerializer.Deserialize<CreateCustomerPaymentRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Oflayn to'lov ma'lumoti noto'g'ri.");
        return c =>
        {
            if (c.Find<MobileOfflineCustomer>(payment.CustomerId) is null)
                throw new InvalidOperationException("Mijoz oflayn keshda topilmadi.");
        };
    }

    private async Task<long> EnqueueAsync(
        string kind,
        string json,
        string idempotencyKey,
        MobileOfflineCredential credential,
        long actorUserId,
        Action<SQLiteConnection>? validate = null,
        Guid? eventId = null,
        DateTime? occurredAt = null)
    {
        await InitializeAsync();
        var cipher = Encrypt(json);
        var sequence = 0L;
        await _db.RunInTransactionAsync(c =>
        {
            if (eventId is { } id)
            {
                var key = id.ToString("D");
                var existing = c.Table<MobileOfflineOutbox>().FirstOrDefault(x => x.EventId == key);
                if (existing is not null)
                {
                    sequence = existing.Sequence;
                    return;
                }
            }

            validate?.Invoke(c);
            var nextRow = c.Find<MobileOfflineMeta>("next_sequence");
            var next = nextRow is not null && long.TryParse(nextRow.Value, out var parsed)
                ? parsed
                : credential.LastAcceptedSequence + 1;
            c.Insert(new MobileOfflineOutbox
            {
                EventId = (eventId ?? Guid.NewGuid()).ToString("D"),
                LeaseId = credential.LeaseId,
                Epoch = credential.Epoch,
                Sequence = next,
                ActorUserId = actorUserId,
                Kind = kind,
                IdempotencyKey = idempotencyKey,
                PayloadCipher = cipher,
                OccurredAt = occurredAt ?? DateTime.UtcNow
            });
            ApplyProjection(c, kind, json, 1);
            Put(c, "next_sequence", (next + 1).ToString());
            sequence = next;
        });
        return sequence;
    }

    public async Task<bool> CancelPendingAsync(int outboxId)
    {
        await InitializeAsync();
        var cancelled = false;
        await _db.RunInTransactionAsync(c =>
        {
            var item = c.Find<MobileOfflineOutbox>(outboxId);
            if (item is null || item.Status != "pending" || item.PushedAt is not null) return;
            ApplyProjection(c, item.Kind, Decrypt(item.PayloadCipher), -1);
            c.Delete(item);
            var later = c.Table<MobileOfflineOutbox>()
                .Where(x => x.LeaseId == item.LeaseId && x.Epoch == item.Epoch && x.Sequence > item.Sequence)
                .OrderBy(x => x.Sequence)
                .ToList();
            foreach (var row in later)
            {
                row.Sequence--;
                c.Update(row);
            }
            var meta = c.Find<MobileOfflineMeta>("next_sequence");
            if (meta is not null && long.TryParse(meta.Value, out var next) && next > item.Sequence)
                Put(c, "next_sequence", (next - 1).ToString());
            cancelled = true;
        });
        return cancelled;
    }

    public async Task MarkPushedAsync(IReadOnlyCollection<int> ids)
    {
        await InitializeAsync();
        var now = DateTime.UtcNow;
        await _db.RunInTransactionAsync(c =>
        {
            foreach (var id in ids)
            {
                var item = c.Find<MobileOfflineOutbox>(id);
                if (item is null || item.PushedAt is not null) continue;
                item.PushedAt = now;
                c.Update(item);
            }
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

    public async Task<List<MobileOfflineSupplier>> SearchSuppliersAsync(string term, int limit)
    {
        await InitializeAsync();
        var normalized = term.Trim().ToLowerInvariant();
        var query = _db.Table<MobileOfflineSupplier>();
        if (normalized.Length > 0)
            query = query.Where(x => x.Name.ToLower().Contains(normalized));
        return await query.OrderBy(x => x.Name).Take(limit).ToListAsync();
    }

    public async Task<List<MobileOfflineOutbox>> GetPendingAsync(long leaseId, long epoch, int limit)
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflineOutbox>()
            .Where(x => x.LeaseId == leaseId && x.Epoch == epoch && x.Status == "pending")
            .OrderBy(x => x.Sequence).Take(limit).ToListAsync();
    }

    public async Task<List<MobileOfflineOutbox>> GetQueueAsync(long leaseId, long epoch, int limit)
    {
        await InitializeAsync();
        return await _db.Table<MobileOfflineOutbox>()
            .Where(x => x.LeaseId == leaseId && x.Epoch == epoch
                && (x.Status == "pending" || x.Status == "error"))
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

    public async Task SetMetaAsync(string key, string value)
    {
        await InitializeAsync();
        await _db.InsertOrReplaceAsync(new MobileOfflineMeta { Key = key, Value = value });
    }

    public async Task RemoveMetaAsync(string key)
    {
        await InitializeAsync();
        await _db.DeleteAsync<MobileOfflineMeta>(key);
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
            c.DeleteAll<MobileOfflineSupplier>();
            c.DeleteAll<MobileOfflineMeta>();
        });
    }

    private static void ApplyProjection(
        SQLiteConnection c,
        string kind,
        string json,
        int sign,
        HashSet<long>? variantIds = null,
        HashSet<long>? customerIds = null)
    {
        switch (kind)
        {
            case "sale.create":
            {
                var sale = JsonSerializer.Deserialize<CreateSaleRequest>(json, JsonOptions);
                if (sale is null) return;
                foreach (var line in sale.Items.GroupBy(x => x.VariantId))
                {
                    if (variantIds?.Contains(line.Key) == false) continue;
                    var product = c.Find<MobileOfflineProduct>(line.Key);
                    if (product is null) continue;
                    product.Quantity -= sign * line.Sum(x => x.Quantity);
                    c.Update(product);
                }
                return;
            }
            case "customer.payment.create":
            {
                var payment = JsonSerializer.Deserialize<CreateCustomerPaymentRequest>(json, JsonOptions);
                if (payment is null) return;
                if (customerIds?.Contains(payment.CustomerId) == false) return;
                var customer = c.Find<MobileOfflineCustomer>(payment.CustomerId);
                if (customer is null) return;
                customer.DebtBalance -= sign * payment.Tenders.Sum(x => x.Amount);
                c.Update(customer);
                return;
            }
            case "supply.create":
            {
                var supply = JsonSerializer.Deserialize<CreateSupplyRequest>(json, JsonOptions);
                if (supply is null) return;
                foreach (var line in supply.Items.Where(x => x.UnitId is null && x.PackId is null))
                {
                    if (variantIds?.Contains(line.VariantId) == false) continue;
                    var product = c.Find<MobileOfflineProduct>(line.VariantId);
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
