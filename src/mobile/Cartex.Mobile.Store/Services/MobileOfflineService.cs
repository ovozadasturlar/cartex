using System.Globalization;
using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.OfflineCache;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;

namespace Cartex.Mobile.Store.Services;

public sealed record MobileOfflineCredential(
    string DeviceId,
    long LeaseId,
    long WarehouseId,
    long Epoch,
    string Token,
    long LastAcceptedSequence);

public sealed record MobileOfflinePaymentDraft(long CustomerId, long? BranchId, decimal Amount, bool ViaCard);

public sealed record MobileOfflineSupplyLineDraft(
    long VariantId, decimal Quantity, decimal PurchasePrice, decimal? SellingPrice);

public sealed record MobileOfflineSupplyDraft(long? SupplierId, long WarehouseId, List<MobileOfflineSupplyLineDraft> Items);

public sealed record MobileOfflineExportEvent(
    string EventId, long Sequence, string Kind, string IdempotencyKey,
    DateTime OccurredAt, long? ActorUserId, JsonElement Payload);

public sealed record MobileOfflineExportFile(
    int CartexOfflineExport, string DeviceId, long LeaseId, long WarehouseId, long Epoch,
    string LeaseToken, DateTime ExportedAt, List<MobileOfflineExportEvent> Events);

public sealed class MobileOfflineService(
    IOfflineCacheApi offlineApi,
    MobileOfflineStore store,
    MobileAuthService auth,
    HubLinkService hubLink,
    WarehouseContext warehouse)
{
    private const string CredentialKey = "offline_lease_v2";
    private const string EnabledKey = "offline_enabled_v2";
    private const string CapSalesKey = "offline_cap_sales";
    private const string CapPaymentsKey = "offline_cap_payments";
    private const string CapSuppliesKey = "offline_cap_supplies";
    private const string SinceKey = "snapshot_since";
    private const string SectionsKey = "snapshot_sections";
    private const string AllSections = "all";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _loopCts;
    private bool _started;
    private MobileOfflineCredential? _credential;
    private MobileOfflineCredential? _satellite;
    private readonly SemaphoreSlim _hubProbeLock = new(1, 1);
    private DateTime _lastHubProbe;
    private int _hubProbeStep;
    private DateTime _lastSnapshotAttempt;
    private DateTime _lastAttestation;

    // Har qidiruv — tarmoqdagi 254 manzilga ulanish urinishi: batareya va router IDS uchun qimmat.
    // Muvaffaqiyatsiz urinishdan keyin oraliq kengayadi, tarmoq o'zgarsa yoki HUB topilsa nolga qaytadi.
    private static readonly TimeSpan[] HubProbeDelays =
        [TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];

    // HUB-05: vakolat boshqa qurilmaga ko'chganini faqat yangi guvohnomadagi `epoch` aytadi.
    private static readonly TimeSpan AttestationInterval = TimeSpan.FromMinutes(5);

    public event Action? StateChanged;
    public bool ServerReachable { get; private set; } = true;
    public MobileOfflineCredential? Credential => _credential;
    public bool IsEnabled => Preferences.Get(EnabledKey, false)
                             && _credential is not null
                             && _credential.DeviceId == auth.DeviceId;

    // HUB-03: vakolat bu qurilmada emas, lekin do'kon HUB'i topilgan — savdo o'sha navbatga yoziladi.
    public bool IsSatellite => _satellite is not null;

    // Ikkala rejimda ham savdo lokal yoziladi; farq faqat qayerga yuborilishida (HUB-07).
    private MobileOfflineCredential? Active => _credential ?? _satellite;

    public bool ShouldUseOffline => (IsEnabled || IsSatellite)
                                    && (Connectivity.Current.NetworkAccess != NetworkAccess.Internet
                                        || !ServerReachable);
    public bool SalesCapability => Preferences.Get(CapSalesKey, true);
    public bool PaymentsCapability => Preferences.Get(CapPaymentsKey, true);
    public bool SuppliesCapability => Preferences.Get(CapSuppliesKey, true);

    public async Task SetCapabilityAsync(string capability, bool value)
    {
        var key = capability switch
        {
            "sales" => CapSalesKey,
            "payments" => CapPaymentsKey,
            _ => CapSuppliesKey
        };
        if (Preferences.Get(key, true) == value) return;
        Preferences.Set(key, value);
        var credential = _credential;
        if (credential is null || !IsEnabled
            || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            StateChanged?.Invoke();
            return;
        }
        await _syncLock.WaitAsync();
        try
        {
            await PullSnapshotAsync(credential);
            ServerReachable = true;
        }
        catch
        {
            ServerReachable = false;
        }
        finally
        {
            _syncLock.Release();
            StateChanged?.Invoke();
        }
    }

    private string? SnapshotSections()
    {
        if (SalesCapability && PaymentsCapability && SuppliesCapability) return null;
        var parts = new List<string>(3);
        if (SalesCapability) parts.Add("sales");
        if (PaymentsCapability) parts.Add("payments");
        if (SuppliesCapability) parts.Add("supplies");
        // Bo'sh csv serverda "hammasi" deb o'qiladi, shuning uchun sentinel kerak.
        return parts.Count == 0 ? "none" : string.Join(',', parts);
    }

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        await store.InitializeAsync();
        _credential = await ReadCredentialAsync();
        if (_credential is not null)
            await store.PrepareLeaseAsync(_credential);
        await hubLink.LoadAsync();
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        // HUB-04: guvohnoma faqat onlayn paytda olinadi — uzilib qolgandan keyin kech bo'ladi.
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = RefreshAttestationAsync();
        // Sikl vakolatsiz qurilmada ham yuradi: guvohnomani yangilab turish uning yagona vazifasi
        // bo'lsa ham, usiz telefon vakolat ko'chganini bilmay eski HUB'ga ulanaverardi.
        StartLoop();
        if (IsEnabled && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
        // Vakolat kaliti va guvohnoma endi o'qildi: HUB xizmati va ekranlar shu holatga qarab
        // qaytadan baholansin — ular servisdan oldin ishga tushgan bo'lishi mumkin.
        StateChanged?.Invoke();
    }

    private async Task RefreshAttestationAsync()
    {
        _lastAttestation = DateTime.UtcNow;
        await hubLink.RefreshAttestationAsync();
    }

    private void StartLoop()
    {
        if (_loopCts is not null) return;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _loopCts = cts;
        _ = RunLoopAsync(cts.Token);
    }

    private void StopLoop()
    {
        Debounce.Cancel(ref _loopCts);
    }

    public void MarkServerUnavailable()
    {
        if (!IsEnabled)
        {
            // HUB-03: vakolat bizda emas — do'kon HUB'i bormi, shuni tekshiramiz.
            _ = Task.Run(() => TryEnterSatelliteAsync(), _lifetime.Token);
            return;
        }
        ServerReachable = false;
        StateChanged?.Invoke();
    }

    // HUB-02/HUB-11: bulut yopilganda do'kon tarmog'idagi HUB qidiriladi va katalog undan olinadi.
    // Qidiruv e'lonni eshitadi va tarmoqni tekshiradi, shuning uchun har bir muvaffaqiyatsiz
    // so'rovdan keyin emas, kengayib boruvchi oraliqda uriniladi.
    public async Task<bool> TryEnterSatelliteAsync()
    {
        if (IsEnabled || IsSatellite || !hubLink.CanLink) return false;
        if (!HubProbeReady()) return false;
        if (!await _hubProbeLock.WaitAsync(0)) return false;
        try
        {
            var entered = await EnterSatelliteCoreAsync();
            HubProbeDone(entered);
            return entered;
        }
        finally
        {
            _hubProbeLock.Release();
        }
    }

    // HUB-12: mobil internetda do'kon tarmog'i yo'q — qidiruv operatorning begona abonentlarini
    // skanerlashdan boshqa narsa qilmaydi.
    private bool HubProbeReady() =>
        HubLinkService.OnLocalNetwork && DateTime.UtcNow - _lastHubProbe >= HubProbeDelays[_hubProbeStep];

    private void HubProbeDone(bool found)
    {
        _lastHubProbe = DateTime.UtcNow;
        _hubProbeStep = found ? 0 : Math.Min(_hubProbeStep + 1, HubProbeDelays.Length - 1);
    }

    private void ResetHubProbe()
    {
        _hubProbeStep = 0;
        _lastHubProbe = DateTime.MinValue;
    }

    // HUB-11: qo'lda ulash (QR) — manzil ma'lum, shuning uchun e'lon kutilmaydi va tarmoq
    // tekshirilmaydi; guvohnoma tekshiruvi esa avtomatik yo'l bilan bir xil.
    public async Task<bool> LinkToHubAsync(Uri endpoint)
    {
        if (IsEnabled || IsSatellite) return false;
        await _hubProbeLock.WaitAsync();
        try
        {
            // Qo'lda ulash — foydalanuvchining aniq buyrug'i: avtomatik qidiruvning kutish
            // oralig'i uni ushlab turmasligi va undan keyin ham qaytarmasligi kerak.
            ResetHubProbe();
            return await hubLink.LinkToAsync(endpoint, _lifetime.Token) && await EnterSatelliteCoreAsync();
        }
        // Avtomatik yo'ldan farqli o'laroq bu chaqiruv ekrandan kutiladi: HUB katalogni berayotib
        // yo'qolsa istisno foydalanuvchi oqimiga chiqib ketmasligi kerak.
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
        finally
        {
            _hubProbeLock.Release();
        }
    }

    private async Task<bool> EnterSatelliteCoreAsync()
    {
        if (!hubLink.IsLinked && !await hubLink.TryLinkAsync(_lifetime.Token)) return false;
        if (hubLink.Hub is not { } hub || hubLink.HubLeaseId == 0) return false;

        var credential = SatelliteCredential(hub.WarehouseId);
        await store.PrepareLeaseAsync(credential);
        // Katalogni almashtirish lokal proyeksiyani qayta yozadi: yuborilmagan qator qolgan
        // bo'lsa uning qoldiqdan ayirgani yo'qoladi va o'sha tovar ikkinchi marta sotilardi.
        // Shu sabab `SatelliteSyncAsync` da ham bor — kirish yo'li ham undan xoli emas.
        if (await store.CountAsync(credential.LeaseId, credential.Epoch, "pending") == 0)
        {
            var snapshot = await hubLink.CatalogAsync(_lifetime.Token);
            if (snapshot is null) return false;
            await store.ReplaceSnapshotAsync(snapshot, credential);
        }
        warehouse.Force(hub.WarehouseId, hub.WarehouseName);
        _satellite = credential;
        ServerReachable = false;
        StartLoop();
        StateChanged?.Invoke();
        return true;
    }

    // HUB-07: navbat HUB'ning lizingiga bog'lanmaydi — vakolat ko'chsa yangi HUB boshqa
    // lease/epoch bilan keladi va eski kalit bilan yozilgan qatorlar ko'rinmay qolardi.
    private MobileOfflineCredential SatelliteCredential(long warehouseId) => new(
        auth.DeviceId, MobileOfflineStore.SatelliteLeaseId, warehouseId,
        MobileOfflineStore.SatelliteEpoch, "", 0);

    // HUB-08: yuborilmagan qator qolgan bo'lsa rejimdan chiqilmaydi — u faqat HUB orqali ketadi.
    public async Task<bool> LeaveSatelliteAsync()
    {
        if (!IsSatellite) return true;
        await SatelliteSyncAsync();
        if (await PendingCountAsync() > 0) return false;
        _satellite = null;
        hubLink.Unlink();
        ServerReachable = true;
        StateChanged?.Invoke();
        return true;
    }

    // HUB yo'qolsa yoki manzilini almashtirsa so'rov istisno bilan tugaydi. U yuqoriga chiqsa
    // 20 soniyalik sikl butunlay to'xtardi — qolgan qatorlar hech qachon yuborilmasdi.
    private async Task<bool> SatelliteSyncAsync()
    {
        try
        {
            return await SatelliteSyncCoreAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                                              or JsonException)
        {
            // Manzil DHCP bilan ko'chgan bo'lishi mumkin: keyingi sikl uni qaytadan qidiradi.
            hubLink.Unlink();
            return false;
        }
    }

    private async Task<bool> SatelliteSyncCoreAsync()
    {
        if (_satellite is not { } credential) return false;
        if (!hubLink.IsLinked)
        {
            if (!HubProbeReady()) return false;
            var found = await hubLink.TryLinkAsync(_lifetime.Token);
            HubProbeDone(found);
            if (!found) return false;
        }

        var rows = await store.GetPendingAsync(credential.LeaseId, credential.Epoch, 50);
        foreach (var row in rows)
        {
            using var payload = JsonDocument.Parse(store.ReadPayload(row));
            var result = await hubLink.SendAsync(new OfflineSyncEventRequest(
                    Guid.Parse(row.EventId), 0, row.Kind, row.IdempotencyKey,
                    row.OccurredAt, payload.RootElement.Clone(), row.ActorUserId),
                _lifetime.Token);
            if (result is null) return false;

            row.PushedAt ??= DateTime.UtcNow;
            // HUB-06: HUB qabul qilgani — hodisa endi uning navbatida, telefon uni qaytarmaydi.
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
            await store.UpdateAsync(row);
        }

        // Katalogni almashtirish lokal proyeksiyani ham qayta yozadi: yuborilmagan qator qolgan
        // bo'lsa uning qoldiqdan ayirgani yo'qoladi va o'sha tovarni ikkinchi marta sotish mumkin
        // bo'lardi. Shuning uchun yangilash faqat navbat bo'shaganda.
        if (await PendingCountAsync() == 0
            && await hubLink.CatalogAsync(_lifetime.Token) is { } snapshot)
            await store.ReplaceSnapshotAsync(snapshot, credential);
        StateChanged?.Invoke();
        return true;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        // Tarmoq almashdi: oldingi tarmoqda yig'ilgan kutish oralig'i bu yerga tegishli emas.
        ResetHubProbe();
        if (auth.UserId is null || e.NetworkAccess != NetworkAccess.Internet || !IsEnabled) return;
        _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (auth.UserId is null) continue;
                var online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
                // HUB-05: guvohnoma bilan birga joriy `epoch` keladi. Uzoq onlayn turgan telefon
                // uni yangilamasa, vakolat boshqa qurilmaga o'tganidan keyin ham eski HUB'ni
                // haqiqiy deb qabul qilardi.
                if (online && DateTime.UtcNow - _lastAttestation >= AttestationInterval)
                    await RefreshAttestationAsync();
                if (IsSatellite)
                {
                    // HUB-11: bulut qaytsa yo'ldosh rejimi o'zi tugaydi — foydalanuvchi aralashmaydi.
                    if (online)
                        await LeaveSatelliteAsync();
                    else
                        await SatelliteSyncAsync();
                    continue;
                }
                if (!IsEnabled || !online) continue;
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

    public async Task ActivateAsync(OfflineLeaseGrantDto grant)
    {
        var credential = new MobileOfflineCredential(
            auth.DeviceId, grant.LeaseId, grant.WarehouseId, grant.Epoch,
            grant.LeaseToken, grant.LastAcceptedSequence);
        await SaveCredentialAsync(credential);
        _credential = credential;
        Preferences.Set(EnabledKey, false);
        await store.PrepareLeaseAsync(credential);
        // OFF-55: yangi vakolat — eski epoch keshi delta bilan tirik qolmasin.
        await store.RemoveMetaAsync(SinceKey);
        await PullSnapshotAsync(credential);
        Preferences.Set(EnabledKey, true);
        ServerReachable = true;
        StartLoop();
        StateChanged?.Invoke();
    }

    public async Task ReleaseAsync(string? reason = null)
    {
        var credential = _credential
            ?? throw new InvalidOperationException("Oflayn vakolat kaliti topilmadi.");
        await offlineApi.ReleaseAsync(new ReleaseOfflineCacheRequest(
            credential.LeaseId, credential.Token, false, reason));
        await DeactivateLocalAsync();
    }

    public async Task DeactivateLocalAsync()
    {
        StopLoop();
        // Ketayotgan sinxronizatsiya lizing kalitini olib yuradi, shuning uchun u
        // tugamaguncha kutiladi — aks holda so'rov almashtirilgan serverga tushishi mumkin.
        await _syncLock.WaitAsync();
        try
        {
            Preferences.Set(EnabledKey, false);
            SecureStorage.Remove(CredentialKey);
            _credential = null;
            await store.ClearProjectionAsync();
        }
        finally
        {
            _syncLock.Release();
        }
        StateChanged?.Invoke();
    }

    public async Task<bool> SyncAsync()
    {
        if (auth.UserId is null || !IsEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return false;
        if (!await _syncLock.WaitAsync(0)) return true;
        try
        {
            var credential = _credential;
            if (credential is null) return false;
            await HeartbeatCoreAsync(credential);
            var pushed = await PushAsync(credential);
            if (pushed)
                await PullSnapshotAsync(_credential ?? credential);
            ServerReachable = true;
            return pushed;
        }
        catch
        {
            ServerReachable = false;
            return false;
        }
        finally
        {
            _syncLock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatAsync()
    {
        if (auth.UserId is null) return;
        if (!await _syncLock.WaitAsync(0)) return;
        try
        {
            if (_credential is { } credential)
            {
                await HeartbeatCoreAsync(credential);
                ServerReachable = true;
            }
        }
        catch
        {
            ServerReachable = false;
        }
        finally
        {
            _syncLock.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task HeartbeatCoreAsync(MobileOfflineCredential credential)
    {
        var pending = await store.CountAsync(credential.LeaseId, credential.Epoch, "pending")
                      + await store.CountAsync(credential.LeaseId, credential.Epoch, "error");
        var result = await offlineApi.HeartbeatAsync(new OfflineHeartbeatRequest(
            credential.LeaseId, credential.Epoch, credential.Token, pending));
        if (result.LastAcceptedSequence != credential.LastAcceptedSequence)
        {
            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            await SaveCredentialAsync(credential);
            _credential = credential;
        }
    }

    private async Task PullSnapshotAsync(MobileOfflineCredential credential)
    {
        _lastSnapshotAttempt = DateTime.UtcNow;
        var sections = SnapshotSections();
        var snapshot = await offlineApi.GetSnapshotAsync(
            credential.LeaseId, credential.Epoch, credential.Token, sections, await SinceAsync(sections));
        if (snapshot.LeaseId != credential.LeaseId || snapshot.Epoch != credential.Epoch)
            throw new InvalidOperationException("Server boshqa oflayn vakolat snapshotini qaytardi.");
        if (snapshot.IsFull)
            await store.ReplaceSnapshotAsync(snapshot, credential);
        else
            await store.ApplyDeltaAsync(snapshot, credential);
        await CommitSinceAsync(snapshot, sections);
    }

    // OFF-53: delta faqat kesh o'sha profil bilan to'plangan bo'lsa so'raladi — profil
    // torayganda server ortiqcha bo'limni "o'chirilgan" deb bilmaydi, u faqat to'liq
    // snapshotda tozalanadi.
    private async Task<DateTime?> SinceAsync(string? sections) =>
        await store.GetMetaAsync(SectionsKey) == (sections ?? AllSections)
        && DateTime.TryParse(await store.GetMetaAsync(SinceKey), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var since)
            ? since
            : null;

    // OFF-54(d): lokal sanoq server sanog'iga mos kelmasa keshda sezilmagan farq bor —
    // chegara tashlanadi va keyingi sikl to'liq snapshot bilan tuzatadi.
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

    private async Task<bool> PushAsync(MobileOfflineCredential credential)
    {
        while (true)
        {
            var rows = await store.GetPendingAsync(credential.LeaseId, credential.Epoch, 50);
            if (rows.Count == 0) return true;
            // Bosh qator xatoda qolgan bo'lsa zanjir uzilgan — qolganlarini yuborish
            // faqat sequence_gap xatolarini ko'paytiradi; foydalanuvchi qarori kutiladi.
            // Teng yoki kichik sequence esa yuboriladi: server EventId bo'yicha dedup qiladi.
            if (rows[0].Sequence > credential.LastAcceptedSequence + 1) return false;

            var pushStamp = DateTime.UtcNow;
            foreach (var row in rows)
                row.PushedAt ??= pushStamp;
            var requests = new List<OfflineSyncEventRequest>(rows.Count);
            foreach (var row in rows)
            {
                using var payload = JsonDocument.Parse(store.ReadPayload(row));
                requests.Add(new OfflineSyncEventRequest(
                    Guid.Parse(row.EventId), row.Sequence, row.Kind, row.IdempotencyKey,
                    row.OccurredAt, payload.RootElement.Clone(), row.ActorUserId));
            }

            await store.MarkPushedAsync(rows.Select(x => x.Id).ToList());
            var result = await offlineApi.SyncBatchAsync(new OfflineSyncBatchRequest(
                credential.LeaseId, credential.Epoch, credential.Token, requests));
            foreach (var eventResult in result.Results)
            {
                var row = rows.FirstOrDefault(x => x.EventId == eventResult.EventId.ToString("D"));
                if (row is null) continue;
                if (eventResult.Status is "Applied" or "AlreadyApplied")
                {
                    row.Status = "done";
                    row.Error = null;
                }
                else if (eventResult.Status == "Rejected")
                {
                    row.Status = "error";
                    row.Error = $"{eventResult.ErrorCode}: {eventResult.Error}";
                }
                else continue;
                await store.UpdateAsync(row);
            }

            credential = credential with { LastAcceptedSequence = result.LastAcceptedSequence };
            await SaveCredentialAsync(credential);
            _credential = credential;
            if (result.Results.Any(x => x.Status == "Rejected")) return false;
        }
    }

    public async Task EnqueueSaleAsync(CreateSaleRequest request)
    {
        if (!SalesCapability)
            throw new InvalidOperationException(Loc.Instance["offline_capability_off"]);
        var (credential, actorId) = RequireLease();
        if (request.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException(Loc.Instance["offline_wrong_warehouse"]);
        var total = Math.Max(0, request.Items.Sum(x => x.Quantity * (x.UnitPrice ?? 0)) - request.DiscountAmount);
        var paid = request.PaidCash + request.PaidCard + request.PaidBonus;
        var debt = Math.Max(0, total - paid);
        if (debt > 0)
        {
            if (request.CustomerId is null)
                throw new InvalidOperationException(Loc.Instance["err_debt_needs_customer"]);
            if (await store.GetMetaAsync("allow_debt_sales") == "0")
                throw new InvalidOperationException(Loc.Instance["debt_sales_disabled"]);
            var customer = await store.GetCustomerAsync(request.CustomerId.Value)
                ?? throw new InvalidOperationException(Loc.Instance["customer"] + " — " + Loc.Instance["offline_not_authority"]);
            if (customer.CreditLimit > 0 && customer.DebtBalance + debt > customer.CreditLimit)
                throw new InvalidOperationException(Loc.Instance["credit_limit_exceeded"]);
        }
        await store.EnqueueSaleAsync(request, credential, actorId);
        StateChanged?.Invoke();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task EnqueuePaymentAsync(MobileOfflinePaymentDraft draft)
    {
        if (!PaymentsCapability)
            throw new InvalidOperationException(Loc.Instance["offline_capability_off"]);
        var (credential, actorId) = RequireLease();
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        var request = new CreateCustomerPaymentRequest(
            draft.CustomerId,
            draft.BranchId,
            [new CustomerPaymentTenderRequest(draft.ViaCard ? "Card" : "Cash", baseCurrency, draft.Amount)],
            IdempotencyKey: Guid.NewGuid().ToString("N"));
        await store.EnqueuePaymentAsync(request, credential, actorId);
        StateChanged?.Invoke();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task EnqueueSupplyAsync(MobileOfflineSupplyDraft draft)
    {
        if (!SuppliesCapability)
            throw new InvalidOperationException(Loc.Instance["offline_capability_off"]);
        var (credential, actorId) = RequireLease();
        if (draft.WarehouseId != credential.WarehouseId)
            throw new InvalidOperationException(Loc.Instance["offline_wrong_warehouse"]);
        var request = new CreateSupplyRequest(
            draft.SupplierId,
            draft.WarehouseId,
            DateOnly.FromDateTime(DateTime.Today),
            draft.Items.Select(x => new CreateSupplyItemRequest(
                x.VariantId, x.Quantity, x.PurchasePrice, null, SellingPrice: x.SellingPrice)).ToList(),
            IdempotencyKey: Guid.NewGuid().ToString("N"));
        await store.EnqueueSupplyAsync(request, credential, actorId);
        StateChanged?.Invoke();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task<List<MobileOfflineOutbox>> GetQueueRowsAsync()
    {
        var active = Active;
        List<MobileOfflineOutbox> rows = active is null
            ? []
            : await store.GetQueueAsync(active.LeaseId, active.Epoch, 500);
        if (!IsSatellite)
            rows.AddRange(await store.GetQueueAsync(
                MobileOfflineStore.SatelliteLeaseId, MobileOfflineStore.SatelliteEpoch, 500));
        return rows;
    }

    // OFF-40: eksport internetsiz ishlaydi — yuborilmagan amallar fayl bo'lib chiqadi.
    public async Task<int> ExportQueueAsync(Stream destination)
    {
        var credential = _credential
            ?? throw new InvalidOperationException(Loc.Instance["offline_not_authority"]);
        var rows = await store.GetQueueAsync(credential.LeaseId, credential.Epoch, 1000);
        var events = new List<MobileOfflineExportEvent>(rows.Count);
        foreach (var row in rows)
        {
            using var payload = JsonDocument.Parse(store.ReadPayload(row));
            events.Add(new MobileOfflineExportEvent(row.EventId, row.Sequence, row.Kind,
                row.IdempotencyKey, row.OccurredAt, row.ActorUserId, payload.RootElement.Clone()));
        }
        var file = new MobileOfflineExportFile(1, credential.DeviceId, credential.LeaseId,
            credential.WarehouseId, credential.Epoch, credential.Token, DateTime.UtcNow, events);
        await JsonSerializer.SerializeAsync(destination, file, Json);
        return events.Count;
    }

    public static async Task<MobileOfflineExportFile> ParseImportFileAsync(Stream source)
    {
        MobileOfflineExportFile? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync<MobileOfflineExportFile>(source, Json);
        }
        catch (JsonException)
        {
            file = null;
        }
        if (file is null || file.CartexOfflineExport != 1 || file.Events.Count == 0)
            throw new InvalidOperationException(Loc.Instance["offline_import_invalid"]);
        return file;
    }

    // OFF-41/43/44: import istalgan qurilmadan; belgilanmaganlari serverda Skipped bo'ladi.
    public async Task<List<OfflineSyncEventResult>> ImportFileAsync(
        MobileOfflineExportFile file, bool skipRejected, IReadOnlyList<Guid>? skipEventIds)
    {
        var events = file.Events.OrderBy(x => x.Sequence).Select(x => new OfflineSyncEventRequest(
            Guid.Parse(x.EventId), x.Sequence, x.Kind, x.IdempotencyKey,
            x.OccurredAt, x.Payload, x.ActorUserId)).ToList();
        var results = new List<OfflineSyncEventResult>();
        foreach (var chunk in events.Chunk(500))
        {
            var batch = await offlineApi.ImportAsync(new OfflineSyncImportRequest(
                file.LeaseId, file.Epoch, file.LeaseToken, chunk, skipRejected, skipEventIds));
            results.AddRange(batch.Results);
            if (!skipRejected && batch.Results.Any(x => x.Status == "Rejected")) break;
        }
        return results;
    }

    public async Task RetryAsync(MobileOfflineOutbox row)
    {
        row.Status = "pending";
        row.Error = null;
        await store.UpdateAsync(row);
        StateChanged?.Invoke();
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task DiscardAsync(MobileOfflineOutbox row, string? reason = null)
    {
        var credential = _credential
            ?? throw new InvalidOperationException(Loc.Instance["offline_not_authority"]);
        try
        {
            using var payload = JsonDocument.Parse(store.ReadPayload(row));
            var result = await offlineApi.SkipAsync(new OfflineSyncSkipRequest(
                credential.LeaseId, credential.Epoch, credential.Token,
                new OfflineSyncEventRequest(Guid.Parse(row.EventId), row.Sequence, row.Kind, row.IdempotencyKey,
                    row.OccurredAt, payload.RootElement.Clone(), row.ActorUserId),
                reason));
            row.Status = "skipped";
            if (result.Sequence > credential.LastAcceptedSequence)
            {
                credential = credential with { LastAcceptedSequence = result.Sequence };
                await SaveCredentialAsync(credential);
                _credential = credential;
            }
        }
        catch (Refit.ApiException ex) when (ex.Content?.Contains("offline_event_already_applied") == true)
        {
            row.Status = "done";
        }
        row.Error = null;
        await store.UpdateAsync(row);
        StateChanged?.Invoke();
        _ = Task.Run(() => SyncAsync(), _lifetime.Token);
    }

    public async Task<bool> CancelAsync(int outboxId)
    {
        var cancelled = await store.CancelPendingAsync(outboxId);
        if (cancelled) StateChanged?.Invoke();
        return cancelled;
    }

    private (MobileOfflineCredential Credential, long ActorId) RequireLease()
    {
        var credential = Active
            ?? throw new InvalidOperationException(Loc.Instance["offline_not_authority"]);
        if (!IsEnabled && !IsSatellite)
            throw new InvalidOperationException(Loc.Instance["offline_not_authority"]);
        var actorId = auth.UserId
            ?? throw new InvalidOperationException(Loc.Instance["err_session_expired"]);
        return (credential, actorId);
    }

    public async Task<IReadOnlyList<SupplierDto>> SearchSuppliersAsync(string term, int limit)
    {
        var rows = await store.SearchSuppliersAsync(term, limit);
        return rows.Select(x => new SupplierDto(x.Id, x.Name, x.Phone, 0)).ToList();
    }

    public async Task<ProductLookupDto?> FindProductAsync(string barcode)
    {
        var found = await store.GetByBarcodeAsync(barcode);
        if (found is null) return null;
        var (product, packQty) = found.Value;
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        return new ProductLookupDto(product.VariantId, product.ProductName, product.UnitName,
            packQty > 0 ? packQty : 1, product.SellingPrice, product.Quantity,
            product.Dimension, AllowsAmountEntry: product.AllowsAmountEntry,
            OriginalSellingPrice: product.SellingPrice,
            PriceCurrency: baseCurrency, BaseCurrency: baseCurrency,
            AllowsFractional: product.AllowsFractional);
    }

    public async Task<ProductLookupDto?> FindProductByVariantAsync(long variantId, decimal packQty = 1)
    {
        var product = await store.GetProductAsync(variantId);
        if (product is null) return null;
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        return new ProductLookupDto(product.VariantId, product.ProductName, product.UnitName,
            packQty > 0 ? packQty : 1, product.SellingPrice, product.Quantity,
            product.Dimension, AllowsAmountEntry: product.AllowsAmountEntry,
            OriginalSellingPrice: product.SellingPrice,
            PriceCurrency: baseCurrency, BaseCurrency: baseCurrency,
            AllowsFractional: product.AllowsFractional);
    }

    public async Task<IReadOnlyList<ProductDto>> SearchProductsAsync(string term, int limit)
    {
        var baseCurrency = await store.GetMetaAsync("base_currency") ?? "UZS";
        var rows = await store.SearchProductsAsync(term, limit);
        return rows.Select(x => new ProductDto(
            x.VariantId, x.VariantId, x.ProductName, x.CategoryName, x.UnitName,
            0, [], null, null, false, null, null, null, null, null,
            x.SellingPrice, x.Quantity, PriceCurrency: baseCurrency,
            Dimension: x.Dimension, IsEnabled: true,
            AllowsAmountEntry: x.AllowsAmountEntry,
            AllowsFractional: x.AllowsFractional)).ToList();
    }

    public async Task<IReadOnlyList<CustomerDto>> SearchCustomersAsync(string term, int limit)
    {
        var rows = await store.SearchCustomersAsync(term, limit);
        var baseCurrency = await BaseCurrencyAsync();
        return rows.Select(x => new CustomerDto(
            x.Id, x.FullName, null, null, x.Phone, null, x.CardBarcode,
            x.DiscountPct, 0, x.DebtBalance, x.CreditLimit)
        {
            DebtBalances = x.DebtBalance > 0
                ? [new Cartex.Shared.Models.Common.CurrencyAmountDto(baseCurrency, x.DebtBalance)]
                : []
        }).ToList();
    }

    public async Task<IReadOnlyList<ParticipantRoleDto>> GetParticipantRolesAsync()
    {
        var rows = await store.GetRolesAsync();
        return rows.Select(x => new ParticipantRoleDto(
            x.Id, x.Key, x.Label, x.Label, true, x.IsRequired, x.CanEqualBuyer,
            x.MaxCount, true, true, x.SortOrder)).ToList();
    }

    public async Task<IReadOnlyList<PartnerDto>> SearchPartnersAsync(string term, int limit)
    {
        var rows = await store.SearchPartnersAsync(term, limit);
        return rows.Select(ToPartner).ToList();
    }

    public async Task<PartnerDto?> FindPartnerByCustomerAsync(long customerId)
    {
        var row = await store.FindPartnerByCustomerAsync(customerId);
        return row is null ? null : ToPartner(row);
    }

    public Task<int> PendingCountAsync() => CountAsync("pending");

    public Task<int> ErrorCountAsync() => CountAsync("error");

    // Yo'ldosh chelagi vakolatdan mustaqil yashaydi: ilova qayta ishga tushganda `_satellite`
    // bo'sh bo'ladi, lekin yuborilmagan qatorlar joyida turadi va sanoqdan tushib qolmasligi kerak.
    private async Task<int> CountAsync(string status)
    {
        var active = Active;
        var count = active is null ? 0 : await store.CountAsync(active.LeaseId, active.Epoch, status);
        return IsSatellite
            ? count
            : count + await store.CountAsync(
                MobileOfflineStore.SatelliteLeaseId, MobileOfflineStore.SatelliteEpoch, status);
    }

    public Task<string?> LastSyncAsync() => store.GetMetaAsync("last_sync");
    public async Task<string> BaseCurrencyAsync() => await store.GetMetaAsync("base_currency") ?? "UZS";

    private static PartnerDto ToPartner(MobileOfflinePartner x) => new(
        x.PartnerId, x.PartyId, x.PartnerCode, x.FullName, x.Phone,
        null, null, x.CustomerId, true, DateOnly.MinValue, 0, 0, 0, 0, null);

    private async Task<MobileOfflineCredential?> ReadCredentialAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(CredentialKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<MobileOfflineCredential>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Task SaveCredentialAsync(MobileOfflineCredential credential) =>
        SecureStorage.SetAsync(CredentialKey, JsonSerializer.Serialize(credential));
}
