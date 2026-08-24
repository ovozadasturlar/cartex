using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Supplies;

namespace Cartex.UI.Services;

public sealed record OfflineSaleItemDraft(long VariantId, decimal Quantity, decimal? UnitPrice);

public sealed record OfflineSaleDraft(
    long WarehouseId,
    long? CustomerId,
    decimal PaidCash,
    decimal PaidCard,
    List<OfflineSaleItemDraft> Items,
    decimal DiscountAmount,
    DateOnly? DebtDueDate = null);

public sealed record OfflinePaymentDraft(long CustomerId, long? BranchId, decimal Amount, bool ViaCard);

public sealed record OfflineExportEvent(
    string EventId, long Sequence, string Kind, string IdempotencyKey,
    DateTime OccurredAt, long? ActorUserId, JsonElement Payload);

public sealed record OfflineExportFile(
    int CartexOfflineExport, string DeviceId, long LeaseId, long WarehouseId, long Epoch,
    string LeaseToken, DateTime ExportedAt, List<OfflineExportEvent> Events);

public sealed record OfflineSupplyLineDraft(
    long VariantId, decimal Quantity, decimal PurchasePrice, DateOnly? ExpiredAt, decimal? SellingPrice);

public sealed record OfflineSupplyDraft(long? SupplierId, long WarehouseId, List<OfflineSupplyLineDraft> Items);

public sealed class OfflineSyncService(
    IOfflineCacheApi offlineApi,
    OfflineStore store,
    OfflineLeaseCredentialStore credentials,
    ConnectivityService connectivity,
    AuthService auth,
    HubClientService hubClient)
{
    private const string SinceKey = "snapshot_since";
    private const string SectionsKey = "snapshot_sections";
    private const string WarehouseKey = "offline_warehouse_id";
    private const string AllSections = "all";
    // HUB-05: `epoch` chegarasi faqat bulutdan yangilanadi — oraliq qancha uzun bo'lsa, vakolat
    // ko'chganini bilmay oflayn qolish ehtimoli shuncha katta.
    private static readonly TimeSpan AttestationInterval = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _hooked;
    private OfflineLeaseCredential? _satellite;
    private readonly SemaphoreSlim _hubProbe = new(1, 1);
    private DateTime _lastHubProbe;
    private DateTime _lastSnapshotAttempt;
    private DateTime _lastAttestation;

    public event Action? StateChanged;

    public OfflineLeaseCredential? Credential => credentials.Load();
    public bool IsEnabled
    {
        get
        {
            var credential = Credential;
            return SettingsService.Instance.OfflineCacheEnabled
                   && credential is not null
                   && credential.DeviceId == SettingsService.Instance.DeviceId
                   && credential.WarehouseId == SettingsService.Instance.OfflineWarehouseId;
        }
    }

    /// HUB-03: vakolat bu kompyuterda emas, lekin do'kon HUB'i topilgan.
    public bool IsSatellite => _satellite is not null;

    /// Ikkala rejimda ham amal lokal navbatga yoziladi; farq faqat qayerga yuborilishida (`HUB-07`).
    private OfflineLeaseCredential? Active => Credential ?? _satellite;

    public bool ShouldUseOffline => (IsEnabled || IsSatellite) && !connectivity.IsOnline;

    /// Oflayn ish uchun amaldagi ombor: vakolat yoki HUB'niki.
    public long? ActiveWarehouseId => Active?.WarehouseId;

    public void Start()
    {
        if (_hooked) return;
        _hooked = true;
        connectivity.PropertyChanged += OnConnectivityChanged;
        _ = RunHeartbeatLoopAsync(_lifetime.Token);
        if (IsEnabled && connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private void OnConnectivityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConnectivityService.IsOnline)) return;
        if (!auth.IsAuthenticated) return;
        if (connectivity.IsOnline)
        {
            if (IsEnabled) _ = Task.Run(() => SyncAsync(), _lifetime.Token);
            _ = Task.Run(() => RefreshAttestationAsync(force: true), _lifetime.Token);
            if (IsSatellite) _ = Task.Run(() => LeaveSatelliteAsync(), _lifetime.Token);
        }
        // HUB-02: vakolat bu qurilmada bo'lmasa, do'kon tarmog'idagi HUB qidiriladi.
        else if (!IsEnabled)
            _ = Task.Run(() => TryEnterSatelliteAsync(), _lifetime.Token);
    }

    /// Qidiruv tarmoqni eshitib, keyin butun tarmoqni tekshirib chiqadi — shuning uchun
    /// tez-tez takrorlanmaydi.
    public async Task<bool> TryEnterSatelliteAsync()
    {
        if (IsEnabled || IsSatellite || !hubClient.CanLink) return false;
        if (DateTime.UtcNow - _lastHubProbe < TimeSpan.FromSeconds(30)) return false;
        if (!await _hubProbe.WaitAsync(0)) return false;
        try
        {
            _lastHubProbe = DateTime.UtcNow;
            if (!await hubClient.TryLinkAsync(_lifetime.Token)) return false;
            if (hubClient.Hub is not { } hub || hubClient.HubLeaseId == 0) return false;

            var credential = new OfflineLeaseCredential(
                SettingsService.Instance.DeviceId, hubClient.HubLeaseId, hub.WarehouseId, hub.Epoch, "", 0);
            await store.PrepareLeaseAsync(credential);
            // HUB-08: vakolat ko'chgan bo'lsa eski bufer qatorlari yangi HUB'ga ko'chiriladi.
            // Faqat ombor o'sha bo'lganda: boshqa ombor hodisasi `HUB-10` bo'yicha baribir rad
            // etiladi va uning qoldiq proyeksiyasi bu katalogga tegishli emas.
            if (await store.GetMetaAsync(WarehouseKey) == hub.WarehouseId.ToString())
                await store.AdoptOutboxAsync(credential);

            // Katalogni almashtirish yuborilmagan qatorning qoldiqdan ayirganini o'chiradi —
            // o'sha tovar ikkinchi marta sotilardi. Bufer bo'shagach uni `SatelliteSyncAsync` oladi.
            if (await store.CountOutboxAsync(credential.LeaseId, credential.Epoch, "pending") == 0)
            {
                var snapshot = await hubClient.CatalogAsync(_lifetime.Token);
                if (snapshot is null) return false;
                await ApplySnapshotAsync(snapshot, credential);
            }
            // HUB-10: ombor HUB'niki — filial konteksti oflaynda shu meta'dan tiklanadi.
            await store.SetMetaAsync("offline_warehouse_name", hub.WarehouseName);
            _satellite = credential;
            StateChanged?.Invoke();
            return true;
        }
        finally
        {
            _hubProbe.Release();
        }
    }

    /// HUB-08: yuborilmagan qator qolgan bo'lsa rejimdan chiqilmaydi — u faqat HUB orqali ketadi.
    public async Task<bool> LeaveSatelliteAsync()
    {
        if (!IsSatellite) return true;
        await SatelliteSyncAsync();
        if (await PendingCountAsync() > 0) return false;
        _satellite = null;
        hubClient.Unlink();
        StateChanged?.Invoke();
        return true;
    }

    private async Task<bool> SatelliteSyncAsync()
    {
        if (_satellite is not { } credential) return false;
        // Ulanish uzilgan bo'lsa rejim boshidan tuziladi: shu orada vakolat boshqa qurilmaga
        // ko'chgan bo'lishi mumkin, bufer qatorlari esa yangi vakolatga o'sha yo'lda ko'chadi.
        if (!hubClient.IsLinked)
        {
            _satellite = null;
            _lastHubProbe = DateTime.MinValue;
            return await TryEnterSatelliteAsync();
        }

        var rows = await store.GetOutboxAsync(credential.LeaseId, credential.Epoch, "pending", 50);
        foreach (var row in rows)
        {
            using var document = JsonDocument.Parse(row.PayloadJson);
            var result = await hubClient.SendAsync(new OfflineSyncEventRequest(
                    Guid.Parse(row.EventId), 0, row.Kind, row.Key,
                    row.OccurredAt, document.RootElement.Clone(), row.ActorUserId),
                _lifetime.Token);
            if (result is null) return false;

            row.PushedAt ??= DateTime.UtcNow;
            // HUB-06: HUB qabul qilgani — hodisa endi uning navbatida, bu qurilma uni qaytarmaydi.
            if (result.Status == "Queued")
            {
                row.Status = "done";
                row.Error = null;
            }
            else
            {
                row.Status = "error";
                row.Error = $"{result.ErrorCode}: {result.Error}";
            }
            await store.UpdateOutboxAsync(row);
        }

        // Katalogni almashtirish lokal proyeksiyani ham qayta yozadi: yuborilmagan qator qolsa
        // uning qoldiqdan ayirgani yo'qolib, o'sha tovar ikkinchi marta sotilishi mumkin edi.
        if (await PendingCountAsync() == 0
            && await hubClient.CatalogAsync(_lifetime.Token) is { } snapshot)
            await ApplySnapshotAsync(snapshot, credential);
        StateChanged?.Invoke();
        return true;
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!auth.IsAuthenticated) continue;
                await RefreshAttestationAsync(force: false);
                if (IsSatellite)
                {
                    // HUB-11: bulut qaytsa yo'ldosh rejimi o'zi tugaydi.
                    if (connectivity.IsOnline) await LeaveSatelliteAsync();
                    else await SatelliteSyncAsync();
                    continue;
                }
                if (!IsEnabled && !connectivity.IsOnline)
                {
                    await TryEnterSatelliteAsync();
                    continue;
                }
                if (!IsEnabled || !connectivity.IsOnline) continue;
                var pending = await PendingCountAsync();
                if (pending > 0 || DateTime.UtcNow - _lastSnapshotAttempt > TimeSpan.FromMinutes(5))
                    await SyncAsync();
                else
                    await HeartbeatAsync();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// HUB-05: uzoq onlayn turgan qurilma ham vakolat ko'chganini bilishi shart — aks holda
    /// oflayn qolganda bulutdan o'qilgan eski `epoch` bilan eskirgan HUB'ni qabul qilardi.
    private async Task RefreshAttestationAsync(bool force)
    {
        if (!auth.IsAuthenticated || !connectivity.IsOnline) return;
        // Davriy yangilash faqat guvohnomasi bor qurilmada: uni olishga ruxsati yo'q foydalanuvchi
        // har besh daqiqada rad javob olib, tarmoqni bekorga bezovta qilardi.
        if (!force && (DateTime.UtcNow - _lastAttestation < AttestationInterval || hubClient.Credential is null))
            return;
        _lastAttestation = DateTime.UtcNow;
        await hubClient.RefreshAttestationAsync();
    }

    public async Task ActivateAsync(OfflineLeaseGrantDto grant, Cartex.Shared.Models.Warehouses.WarehouseDto? warehouse = null)
    {
        var credential = new OfflineLeaseCredential(
            SettingsService.Instance.DeviceId,
            grant.LeaseId,
            grant.WarehouseId,
            grant.Epoch,
            grant.LeaseToken,
            grant.LastAcceptedSequence);
        credentials.Save(credential);
        SettingsService.Instance.OfflineWarehouseId = grant.WarehouseId;
        SettingsService.Instance.OfflineCacheEnabled = false;
        await store.PrepareLeaseAsync(credential);
        // OFF-55: yangi vakolat — eski epoch keshi delta bilan tirik qolmasin.
        await store.RemoveMetaAsync(SinceKey);
        if (warehouse is not null)
        {
            await store.SetMetaAsync("offline_warehouse_name", warehouse.Name);
            await store.SetMetaAsync("offline_branch_id", warehouse.BranchId.ToString());
            await store.SetMetaAsync("offline_branch_name", warehouse.BranchName ?? "");
        }
        await PullSnapshotAsync(credential);
        SettingsService.Instance.OfflineCacheEnabled = true;
        StateChanged?.Invoke();
    }

    public async Task ReleaseAsync(string? reason = null)
    {
        var credential = Credential
            ?? throw new InvalidOperationException("Oflayn vakolat kaliti topilmadi.");
        await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
            credential.LeaseId, credential.Token, false, reason));
        await DeactivateLocalAsync();
    }

    public async Task DeactivateLocalAsync()
    {
        SettingsService.Instance.OfflineCacheEnabled = false;
        credentials.Clear();
        await store.ClearAsync();
        StateChanged?.Invoke();
    }

    public async Task<bool> SyncAsync()
    {
        if (!auth.IsAuthenticated || !IsEnabled || !connectivity.IsOnline) return false;
        if (!await _lock.WaitAsync(0)) return true;
        try
        {
            var credential = Credential;
            if (credential is null) return false;
            await HeartbeatCoreAsync(credential);
            var pushedAll = await PushOutboxAsync(credential);
            if (pushedAll)
            {
                credential = Credential ?? credential;
                await PullSnapshotAsync(credential);
            }
            return pushedAll;
        }
        catch
        {
            return false;
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatAsync()
    {
        if (!auth.IsAuthenticated) return;
        if (!await _lock.WaitAsync(0)) return;
        try
        {
            if (Credential is { } credential)
                await HeartbeatCoreAsync(credential);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatCoreAsync(OfflineLeaseCredential credential)
    {
        var pending = await store.CountOutboxAsync(credential.LeaseId, credential.Epoch, "pending")
                      + await store.CountOutboxAsync(credential.LeaseId, credential.Epoch, "error");
        var response = await offlineApi.HeartbeatAsync(new OfflineHeartbeatRequest(
            credential.LeaseId, credential.Epoch, credential.Token, pending));
        if (response.LastAcceptedSequence != credential.LastAcceptedSequence)
            credentials.Save(credential with { LastAcceptedSequence = response.LastAcceptedSequence });
    }

    // OFF-51: kesh faqat yoqilgan imkoniyatlar uchun kerakli bo'limlarni tortadi.
    private static string? ProfileSections()
    {
        var settings = SettingsService.Instance;
        if (settings.OfflineAllowSales && settings.OfflineAllowPayments && settings.OfflineAllowSupplies)
            return null;
        var parts = new List<string>(3);
        if (settings.OfflineAllowSales) parts.Add("sales");
        if (settings.OfflineAllowPayments) parts.Add("payments");
        if (settings.OfflineAllowSupplies) parts.Add("supplies");
        return parts.Count == 0 ? "none" : string.Join(',', parts);
    }

    public async Task RepullSnapshotAsync()
    {
        if (!IsEnabled || !connectivity.IsOnline) return;
        if (!await _lock.WaitAsync(0)) return;
        try
        {
            if (Credential is { } credential)
                await PullSnapshotAsync(credential);
        }
        catch
        {
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task PullSnapshotAsync(OfflineLeaseCredential credential)
    {
        _lastSnapshotAttempt = DateTime.UtcNow;
        var sections = ProfileSections();
        var snapshot = await offlineApi.GetSnapshotAsync(
            credential.LeaseId, credential.Epoch, credential.Token, sections, await SinceAsync(sections));
        if (snapshot.LeaseId != credential.LeaseId || snapshot.Epoch != credential.Epoch)
            throw new InvalidOperationException("Server boshqa oflayn vakolat snapshotini qaytardi.");

        await ApplySnapshotAsync(snapshot, credential);
        await CommitSinceAsync(snapshot, sections);
    }

    /// Bir xil xaritalash ikki manba uchun: bulut snapshoti va HUB katalogi (`HUB-09`).
    private async Task ApplySnapshotAsync(OfflineSnapshotDto snapshot, OfflineLeaseCredential credential)
    {
        var products = snapshot.Products.Select(p => new OfflineProduct
        {
            VariantId = p.VariantId,
            ProductName = p.ProductName,
            CategoryName = p.CategoryName,
            UnitName = p.UnitName,
            Quantity = p.Quantity,
            SellingPrice = p.SellingPrice,
            AllowsAmountEntry = p.AllowsAmountEntry,
            AllowsFractional = p.AllowsFractional
        }).ToList();
        var barcodes = snapshot.Barcodes.Select(b => new OfflineBarcode
        {
            Code = b.Code,
            VariantId = b.VariantId,
            PackQty = b.PackQty
        }).ToList();
        var customers = snapshot.Customers.Select(c => new OfflineCustomer
        {
            Id = c.Id,
            FullName = c.FullName,
            Phone = c.Phone,
            CardBarcode = c.CardBarcode,
            DiscountPct = c.DiscountPct,
            DebtBalance = c.DebtBalance,
            CreditLimit = c.CreditLimit
        }).ToList();
        var suppliers = (snapshot.Suppliers ?? []).Select(s => new OfflineSupplier
        {
            Id = s.Id,
            Name = s.Name,
            Phone = s.Phone
        }).ToList();

        if (snapshot.IsFull)
            await store.ReplaceSnapshotAsync(products, barcodes, customers, suppliers,
                credential.LeaseId, credential.Epoch, snapshot.SnapshotVersion);
        else
            await store.ApplyDeltaAsync(products, barcodes, customers, suppliers,
                new OfflineSnapshotRemovals(snapshot.RemovedProductIds, snapshot.RemovedBarcodeCodes,
                    snapshot.RemovedCustomerIds, snapshot.RemovedSupplierIds),
                credential.LeaseId, credential.Epoch, snapshot.SnapshotVersion);

        // Katalog qaysi omborniki: yuborilmagan qatorlarni yangi vakolatga ko'chirish shunga tayanadi.
        await store.SetMetaAsync(WarehouseKey, credential.WarehouseId.ToString());
        await store.SetMetaAsync("base_currency", snapshot.BaseCurrency);
        await store.SetMetaAsync("allow_debt_sales", snapshot.AllowDebtSales ? "1" : "0");
        await store.SetMetaAsync("allow_insufficient_stock_sales",
            snapshot.AllowInsufficientStockSales ? "1" : "0");
        await store.SetMetaAsync("last_sync", snapshot.ServerTime.ToLocalTime().ToString("dd.MM.yyyy HH:mm"));
    }

    /// OFF-53: delta faqat kesh o'sha profil bilan to'plangan bo'lsa so'raladi — profil
    /// torayganda server ortiqcha bo'limni "o'chirilgan" deb bilmaydi, u faqat to'liq
    /// snapshotda tozalanadi.
    private async Task<DateTime?> SinceAsync(string? sections) =>
        await store.GetMetaAsync(SectionsKey) == (sections ?? AllSections)
        && DateTime.TryParse(await store.GetMetaAsync(SinceKey), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var since)
            ? since
            : null;

    /// OFF-54(d): lokal sanoq server sanog'iga mos kelmasa keshda sezilmagan farq bor —
    /// chegara tashlanadi va keyingi sikl to'liq snapshot bilan tuzatadi.
    private async Task CommitSinceAsync(OfflineSnapshotDto snapshot, string? sections)
    {
        var totals = snapshot.Totals;
        if (await store.CountSnapshotAsync()
            != (totals.Products, totals.Barcodes, totals.Customers, totals.Suppliers))
        {
            await store.RemoveMetaAsync(SinceKey);
            return;
        }
        await store.SetMetaAsync(SinceKey, snapshot.ServerTime.ToUniversalTime().ToString("O"));
        await store.SetMetaAsync(SectionsKey, sections ?? AllSections);
    }

    private async Task<bool> PushOutboxAsync(OfflineLeaseCredential credential)
    {
        while (true)
        {
            var rows = await store.GetOutboxAsync(
                credential.LeaseId, credential.Epoch, "pending", 50);
            if (rows.Count == 0) return true;
            // Bosh qator xatoda qolgan bo'lsa zanjir uzilgan — qolganlarini yuborish
            // faqat sequence_gap xatolarini ko'paytiradi; foydalanuvchi qarori kutiladi.
            // Teng yoki kichik sequence esa yuboriladi: server EventId bo'yicha dedup qiladi.
            if (rows[0].Sequence > credential.LastAcceptedSequence + 1) return false;

            var pushStamp = DateTime.UtcNow;
            foreach (var item in rows)
                item.PushedAt ??= pushStamp;
            var eventRows = new List<OfflineSyncEventRequest>(rows.Count);
            foreach (var item in rows)
            {
                using var document = JsonDocument.Parse(item.PayloadJson);
                eventRows.Add(new OfflineSyncEventRequest(
                    Guid.Parse(item.EventId),
                    item.Sequence,
                    item.Kind,
                    item.Key,
                    item.OccurredAt,
                    document.RootElement.Clone(),
                    item.ActorUserId));
            }

            await store.MarkPushedAsync(rows.Select(x => x.Id).ToList());
            var result = await offlineApi.SyncBatchAsync(new OfflineSyncBatchRequest(
                credential.LeaseId, credential.Epoch, credential.Token, eventRows));
            foreach (var eventResult in result.Results)
            {
                var item = rows.FirstOrDefault(x => x.EventId == eventResult.EventId.ToString("D"));
                if (item is null) continue;
                switch (eventResult.Status)
                {
                    case "Applied":
                    case "AlreadyApplied":
                        item.Status = "done";
                        item.Error = null;
                        break;
                    case "Rejected":
                        item.Status = "error";
                        item.Error = $"{eventResult.ErrorCode}: {eventResult.Error}";
                        break;
                    default:
                        continue;
                }
                await store.UpdateOutboxAsync(item);
            }

            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            credentials.Save(credential);
            if (result.Results.Any(x => x.Status == "Rejected")) return false;
        }
    }

    public async Task EnqueueSaleAsync(OfflineSaleDraft draft)
    {
        if (!SettingsService.Instance.OfflineAllowSales)
            throw new InvalidOperationException("Oflayn savdo bu qurilma profilida o'chirilgan.");
        var credential = Active
            ?? throw new InvalidOperationException("Bu qurilmada oflayn savdo vakolati yo'q.");
        if ((!IsEnabled && !IsSatellite) || draft.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException("Savdo faqat oflayn vakolatga biriktirilgan omborda bajariladi.");
        var actorId = auth.UserInfo?.UserId ?? 0;
        if (actorId <= 0)
            throw new InvalidOperationException("Oflayn savdo foydalanuvchisi aniqlanmadi.");

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = new CreateSaleRequest(
            draft.WarehouseId,
            draft.CustomerId,
            draft.PaidCash,
            draft.PaidCard,
            0,
            draft.Items.Select(x => new CreateSaleItemRequest(x.VariantId, x.Quantity, x.UnitPrice)).ToList())
        {
            DiscountAmount = draft.DiscountAmount,
            DebtDueDate = draft.DebtDueDate,
            IdempotencyKey = idempotencyKey,
            ApplyAutoDiscount = false,
            UseCustomerAdvance = false
        };
        var payload = JsonSerializer.Serialize(request, Json);
        await store.EnqueueSaleAndAdjustStockAsync(payload, idempotencyKey, credential, actorId,
            await store.GetAllowInsufficientStockSalesAsync());
        StateChanged?.Invoke();
        if (connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task EnqueuePaymentAsync(OfflinePaymentDraft draft)
    {
        if (!SettingsService.Instance.OfflineAllowPayments)
            throw new InvalidOperationException("Oflayn to'lov bu qurilma profilida o'chirilgan.");
        var (credential, actorId) = RequireLease();
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = new CreateCustomerPaymentRequest(
            draft.CustomerId,
            draft.BranchId,
            [new CustomerPaymentTenderRequest(draft.ViaCard ? "Card" : "Cash", baseCurrency, draft.Amount)],
            IdempotencyKey: idempotencyKey);
        var payload = JsonSerializer.Serialize(request, Json);
        await store.EnqueuePaymentAndAdjustDebtAsync(payload, idempotencyKey, credential, actorId);
        StateChanged?.Invoke();
        if (connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task EnqueueSupplyAsync(OfflineSupplyDraft draft)
    {
        if (!SettingsService.Instance.OfflineAllowSupplies)
            throw new InvalidOperationException("Oflayn kirim bu qurilma profilida o'chirilgan.");
        var (credential, actorId) = RequireLease();
        if (draft.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException("Kirim faqat oflayn vakolatga biriktirilgan omborga qilinadi.");
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var request = new CreateSupplyRequest(
            draft.SupplierId,
            draft.WarehouseId,
            DateOnly.FromDateTime(DateTime.Today),
            draft.Items.Select(x => new CreateSupplyItemRequest(
                x.VariantId, x.Quantity, x.PurchasePrice, x.ExpiredAt, null, x.SellingPrice)).ToList(),
            IdempotencyKey: idempotencyKey);
        var payload = JsonSerializer.Serialize(request, Json);
        await store.EnqueueSupplyAndAdjustStockAsync(payload, idempotencyKey, credential, actorId);
        StateChanged?.Invoke();
        if (connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    /// Joriy vakolatning navbati va undan tashqarida qolgan qatorlar birga: ikkinchisi hech
    /// qayerga yuborilmaydi, lekin unda ham pul turadi — ko'rinmasa foydalanuvchi bilmay qoladi.
    public async Task<List<OfflineOutboxItem>> GetQueueRowsAsync()
    {
        var (leaseId, epoch) = ActiveLease;
        var rows = new List<OfflineOutboxItem>();
        if (leaseId != 0)
            rows.AddRange((await store.GetOutboxAsync(leaseId, epoch, null, 500))
                .Where(x => x.Status is "pending" or "error"));
        rows.AddRange(await store.GetOrphanOutboxAsync(leaseId, epoch, 500));
        return rows;
    }

    public (long LeaseId, long Epoch) ActiveLease
    {
        get
        {
            var credential = Active;
            return (credential?.LeaseId ?? 0, credential?.Epoch ?? 0);
        }
    }

    public async Task RetryAsync(OfflineOutboxItem item)
    {
        item.Status = "pending";
        item.Error = null;
        await store.UpdateOutboxAsync(item);
        StateChanged?.Invoke();
        if (connectivity.IsOnline)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task DiscardAsync(OfflineOutboxItem item, string? reason = null)
    {
        var credential = Credential
            ?? throw new InvalidOperationException("Bu qurilmada oflayn vakolat yo'q.");
        try
        {
            using var document = JsonDocument.Parse(item.PayloadJson);
            var result = await offlineApi.SkipAsync(new OfflineSyncSkipRequest(
                credential.LeaseId, credential.Epoch, credential.Token,
                new OfflineSyncEventRequest(Guid.Parse(item.EventId), item.Sequence, item.Kind, item.Key,
                    item.OccurredAt, document.RootElement.Clone(), item.ActorUserId),
                reason));
            item.Status = "skipped";
            if (result.Sequence > credential.LastAcceptedSequence)
                credentials.Save(credential with { LastAcceptedSequence = result.Sequence });
        }
        catch (Refit.ApiException ex) when (ex.Content?.Contains("offline_event_already_applied") == true)
        {
            item.Status = "done";
        }
        item.Error = null;
        await store.UpdateOutboxAsync(item);
        StateChanged?.Invoke();
        _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task<bool> CancelAsync(int outboxId)
    {
        var cancelled = await store.CancelPendingAsync(outboxId);
        if (cancelled) StateChanged?.Invoke();
        return cancelled;
    }

    // OFF-40: eksport internetsiz ishlaydi — yuborilmagan amallar fayl bo'lib chiqadi.
    public async Task<int> ExportQueueAsync(Stream destination)
    {
        var credential = Credential
            ?? throw new InvalidOperationException("Bu qurilmada oflayn vakolat yo'q.");
        var rows = await store.GetOutboxAsync(credential.LeaseId, credential.Epoch, null, 1000);
        var events = new List<OfflineExportEvent>();
        foreach (var row in rows.Where(x => x.Status is "pending" or "error").OrderBy(x => x.Sequence))
        {
            using var document = JsonDocument.Parse(row.PayloadJson);
            events.Add(new OfflineExportEvent(row.EventId, row.Sequence, row.Kind, row.Key,
                row.OccurredAt, row.ActorUserId, document.RootElement.Clone()));
        }
        var file = new OfflineExportFile(1, credential.DeviceId, credential.LeaseId,
            credential.WarehouseId, credential.Epoch, credential.Token, DateTime.UtcNow, events);
        await JsonSerializer.SerializeAsync(destination, file, Json);
        return events.Count;
    }

    public async Task<OfflineExportFile> ParseExportFileAsync(Stream source)
    {
        var file = await JsonSerializer.DeserializeAsync<OfflineExportFile>(source, Json)
            ?? throw new InvalidOperationException("Fayl formati noto'g'ri.");
        if (file.CartexOfflineExport != 1 || file.Events.Count == 0)
            throw new InvalidOperationException("Fayl formati noto'g'ri.");
        return file;
    }

    // OFF-41/43/44: import istalgan qurilmadan; tanlovdan chiqarilganlari serverda
    // Skipped bo'lib qayd etiladi — izsiz o'chirish yo'q.
    public async Task<List<OfflineSyncEventResult>> ImportFileAsync(
        OfflineExportFile file, IReadOnlyCollection<Guid>? skipEventIds, bool skipRejected)
    {
        var events = file.Events.Select(x => new OfflineSyncEventRequest(
            Guid.Parse(x.EventId), x.Sequence, x.Kind, x.IdempotencyKey,
            x.OccurredAt, x.Payload, x.ActorUserId)).ToList();
        var results = new List<OfflineSyncEventResult>();
        foreach (var chunk in events.Chunk(500))
        {
            var skipInChunk = skipEventIds is null
                ? null
                : chunk.Where(x => skipEventIds.Contains(x.EventId)).Select(x => x.EventId).ToList();
            var batch = await offlineApi.ImportAsync(new OfflineSyncImportRequest(
                file.LeaseId, file.Epoch, file.LeaseToken, chunk, skipRejected,
                skipInChunk is { Count: > 0 } ? skipInChunk : null));
            results.AddRange(batch.Results);
            if (!skipRejected && batch.Results.Any(x => x.Status == "Rejected")) break;
        }
        return results;
    }

    private (OfflineLeaseCredential Credential, long ActorId) RequireLease()
    {
        var credential = Active
            ?? throw new InvalidOperationException("Bu qurilmada oflayn vakolat yo'q.");
        if (!IsEnabled && !IsSatellite)
            throw new InvalidOperationException("Oflayn rejim faol emas.");
        var actorId = auth.UserInfo?.UserId ?? 0;
        return actorId > 0
            ? (credential, actorId)
            : throw new InvalidOperationException("Oflayn amal foydalanuvchisi aniqlanmadi.");
    }

    public async Task<int> PendingCountAsync()
    {
        var credential = Active;
        return credential is null ? 0 : await store.CountOutboxAsync(
            credential.LeaseId, credential.Epoch, "pending");
    }

    public async Task<int> ErrorCountAsync()
    {
        var credential = Active;
        return credential is null ? 0 : await store.CountOutboxAsync(
            credential.LeaseId, credential.Epoch, "error");
    }

    /// HUB-08: vakolat ko'chgani uchun joriy navbatga tushmay qolgan qatorlar.
    public async Task<int> OrphanCountAsync()
    {
        var (leaseId, epoch) = ActiveLease;
        return await store.CountOrphanOutboxAsync(leaseId, epoch);
    }

    public Task<string?> LastSyncTextAsync() => store.GetMetaAsync("last_sync");
}
