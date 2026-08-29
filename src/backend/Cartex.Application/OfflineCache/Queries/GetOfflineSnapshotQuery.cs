using Cartex.Application.Common.Catalog;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Stocks.Queries;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.OfflineCache;
using Microsoft.EntityFrameworkCore;
using SharedStockOnHandDto = Cartex.Shared.Models.Stocks.StockOnHandDto;

namespace Cartex.Application.OfflineCache.Queries;

public sealed record GetOfflineSnapshotQuery(
    long LeaseId,
    long Epoch,
    string LeaseToken,
    bool IncludeSales = true,
    bool IncludePayments = true,
    bool IncludeSupplies = true,
    DateTime? Since = null) : ICommand<OfflineSnapshotDto>;

public sealed class GetOfflineSnapshotQueryHandler(
    IApplicationDbContext db,
    ISettingsService settings,
    ICurrentUser currentUser,
    ISender sender)
    : IRequestHandler<GetOfflineSnapshotQuery, OfflineSnapshotDto>
{
    /// OFF-54(b): bitta tranzaksiyada yozilgan qatorlar tushib qolmasligi uchun oyna orqaga suriladi.
    /// Takror kelgan qator klientda idempotent upsert bilan yutiladi.
    // Yozuv vaqti tranzaksiya boshida qo'yiladi, commit esa kech bo'lishi mumkin (katta import) —
    // oyna shu farqni qoplaydi. Takror kelgan qator upsert bilan yutiladi, tushib qolgani esa yo'q.
    private static readonly TimeSpan DeltaOverlap = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DeltaMaxAge = TimeSpan.FromDays(7);

    public async Task<OfflineSnapshotDto> Handle(GetOfflineSnapshotQuery request, CancellationToken cancellationToken)
    {
        var lease = await OfflineLeaseSecurity.RequireActiveAsync(db, currentUser,
            request.LeaseId, request.Epoch, request.LeaseToken, true, cancellationToken);
        var policy = await settings.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken)
            ?? new SalesPolicySettings();
        var baseCode = await db.Businesses
            .Where(x => x.Id == lease.BusinessId)
            .Select(x => x.Currency)
            .SingleAsync(cancellationToken);

        // OFF-54(a): chegara server soatida hisoblanadi, klient soatiga ishonilmaydi.
        var now = DateTime.UtcNow;
        var threshold = await DeltaThresholdAsync(request.Since, lease, now, cancellationToken);

        // OFF-51: kesh faqat yoqilgan imkoniyatlar uchun kerakli bo'limlarni oladi.
        var includeProducts = request.IncludeSales || request.IncludeSupplies;
        var includeCustomers = request.IncludeSales || request.IncludePayments;

        var products = new List<SharedStockOnHandDto>();
        long[] removedProductIds = [];
        if (includeProducts)
        {
            List<long>? changed = threshold is null
                ? null
                : await ChangedVariantIdsAsync(lease, threshold.Value, cancellationToken);

            if (changed is null || changed.Count > 0)
                for (var page = 1; ; page++)
                {
                    var chunk = await sender.Send(new GetStockOnHandQuery(
                        lease.WarehouseId, Page: page, PageSize: 1000, ForSale: true, VariantIds: changed), cancellationToken);
                    products.AddRange(chunk.Items.Select(x => new SharedStockOnHandDto(
                        x.VariantId, x.ProductName, x.CategoryId, x.CategoryName, x.UnitName,
                        x.Dimension, x.Quantity, x.SellingPrice, x.NearestExpiry, x.ImageUrl,
                        x.DiscountPct, x.Code, x.Barcodes, x.AllowsAmountEntry,
                        x.AllowsFractional)));
                    if (chunk.Items.Count < 1000) break;
                }

            if (changed is not null)
                removedProductIds = Removed(changed, products.Select(x => x.VariantId));
        }

        var variantIds = products.Select(x => x.VariantId).ToHashSet();
        var barcodes = variantIds.Count == 0
            ? []
            : await db.Barcodes.AsNoTracking()
                .Where(x => variantIds.Contains(x.VariantId))
                .Select(x => new OfflineBarcodeDto(x.VariantId, x.Code, x.PackQty))
                .ToListAsync(cancellationToken);
        List<string> removedBarcodeCodes = threshold is null || !includeProducts
            ? []
            : await RemovedBarcodeCodesAsync(threshold.Value, removedProductIds, cancellationToken);

        List<OfflineCustomerDto> customers = [];
        long[] removedCustomerIds = [];
        if (includeCustomers)
        {
            long[]? changed = threshold is null
                ? null
                : await ChangedCustomerIdsAsync(threshold.Value, cancellationToken);
            if (changed is null || changed.Length > 0)
            {
                var rows = db.Customers.AsNoTracking();
                if (changed is not null)
                    rows = rows.Where(x => changed.Contains(x.Id));
                customers = await rows
                    .OrderBy(x => x.Party.FullName)
                    .Select(x => new OfflineCustomerDto(
                        x.Id,
                        x.Party.FullName,
                        x.Party.Phone,
                        x.CardBarcode,
                        x.DiscountPct,
                        x.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a =>
                            a.Balance * (a.Currency == baseCode
                                ? 1m
                                : db.ExchangeRates.Where(r => r.Code == a.Currency)
                                    .OrderByDescending(r => r.EffectiveAt)
                                    .Select(r => r.Rate)
                                    .FirstOrDefault())),
                        x.CreditLimit))
                    .ToListAsync(cancellationToken);
            }
            if (changed is not null)
                removedCustomerIds = Removed(changed, customers.Select(x => x.Id));
        }

        var roles = !request.IncludeSales
            ? []
            : await db.ParticipantRoleDefinitions.AsNoTracking()
                .Where(x => x.BusinessId == lease.BusinessId && x.IsEnabled && x.AppliesToCart)
                .OrderBy(x => x.SortOrder)
                .Select(x => new OfflineParticipantRoleDto(
                    x.Id, x.Key, x.SingularLabel, x.IsRequired,
                    x.CanEqualBuyer, x.MaxCount, x.SortOrder))
                .ToListAsync(cancellationToken);
        var partners = !request.IncludeSales
            ? []
            : await db.PartnerProfiles.AsNoTracking()
                .Where(x => x.Party.BusinessId == lease.BusinessId && x.IsEnabled)
                .OrderBy(x => x.Party.FullName)
                .Select(x => new OfflinePartnerDto(
                    x.Id, x.PartyId, x.PartnerCode, x.Party.FullName,
                    x.Party.Phone, x.Party.CustomerProfile == null
                        ? null
                        : (long?)x.Party.CustomerProfile.Id))
                .ToListAsync(cancellationToken);

        List<OfflineSupplierDto> suppliers = [];
        long[] removedSupplierIds = [];
        if (request.IncludeSupplies)
        {
            long[]? changed = threshold is null
                ? null
                : await ChangedSince(db.Suppliers, threshold.Value).Select(x => x.Id).ToArrayAsync(cancellationToken);
            if (changed is null || changed.Length > 0)
            {
                var rows = db.Suppliers.AsNoTracking();
                if (changed is not null)
                    rows = rows.Where(x => changed.Contains(x.Id));
                suppliers = await rows
                    .OrderBy(x => x.Name)
                    .Select(x => new OfflineSupplierDto(x.Id, x.Name, x.Phone))
                    .ToListAsync(cancellationToken);
            }
            if (changed is not null)
                removedSupplierIds = Removed(changed, suppliers.Select(x => x.Id));
        }

        var totals = threshold is null
            ? new OfflineSnapshotTotals(products.Count, barcodes.Count, customers.Count, suppliers.Count)
            : await CacheTotalsAsync(lease, policy, includeProducts, includeCustomers,
                request.IncludeSupplies, cancellationToken);

        lease.LastHeartbeatAt = now;
        lease.Version++;
        await db.SaveChangesAsync(cancellationToken);

        return new OfflineSnapshotDto(baseCode, now, products, barcodes, customers,
            policy.AllowDebtSales, policy.AllowInsufficientStockSales,
            lease.Id, lease.Epoch, now.Ticks, roles, partners, suppliers)
        {
            IsFull = threshold is null,
            RemovedProductIds = removedProductIds,
            RemovedCustomerIds = removedCustomerIds,
            RemovedSupplierIds = removedSupplierIds,
            RemovedBarcodeCodes = removedBarcodeCodes,
            Totals = totals,
        };
    }

    /// OFF-53/OFF-55: delta faqat server to'liqlikni kafolatlay olganda beriladi. `null` — to'liq snapshot.
    private async Task<DateTime?> DeltaThresholdAsync(
        DateTime? since, OfflineAuthorityLease lease, DateTime now, CancellationToken cancellationToken)
    {
        var from = Utc(since);
        // Vakolat qayta olinganda `ClaimedAt` yangilanadi va `Epoch` o'sadi — undan oldingi
        // baza bilan solishtirish eski epoch keshini sezdirmay tirik qoldirardi (OFF-55).
        // Kelajakdagi chegara (soat siljishi yoki buzilgan meta) oynani bo'shatib, narx
        // o'zgarishini yashirardi — sanoq bunday farqni ko'rmaydi, shuning uchun to'liq snapshot.
        if (from is null || from < lease.ClaimedAt || from > now || now - from > DeltaMaxAge)
            return null;

        // Oyna vakolat olingan lahzadan orqaga surilmaydi: undan oldingi hamma narsa klientning
        // majburiy birinchi to'liq snapshotida bor. Aks holda vakolat olingan birinchi daqiqadagi
        // har bir delta butun katalogni qaytadan tortib olardi.
        var threshold = Max(from.Value - DeltaOverlap, lease.ClaimedAt);
        return await CatalogWideChangeAsync(threshold, cancellationToken) ? null : threshold;
    }

    /// Bu jadvallarning bitta qatori butun katalogning kesh qatorlarini o'zgartiradi (kategoriya
    /// nomi, o'lchov birligi, chegirma qoidasi, valyuta kursi, savdo siyosati). Ularni qator-qator
    /// kuzatib bo'lmaydi, shuning uchun bunday o'zgarish deltani emas, to'liq snapshotni talab qiladi.
    private async Task<bool> CatalogWideChangeAsync(DateTime threshold, CancellationToken cancellationToken) =>
        await ChangedSince(db.Categories, threshold).AnyAsync(cancellationToken)
        || await ChangedSince(db.Units, threshold).AnyAsync(cancellationToken)
        || await ChangedSince(db.DiscountRules, threshold).AnyAsync(cancellationToken)
        || await ChangedSince(db.BusinessSettings, threshold).AnyAsync(x => x.Key == SettingKeys.SalesPolicy, cancellationToken)
        || await db.ExchangeRates.AsNoTracking().AnyAsync(x => x.EffectiveAt > threshold, cancellationToken);

    /// OFF-54(c): kartochka, variant, qoldiq, shtrix-kod, filial assortimenti yoki narx qatori.
    /// Narx qoida matnida sanalmagan, lekin u kesh qatorining bir qismi — usiz do'kon eski narxda sotardi.
    private async Task<List<long>> ChangedVariantIdsAsync(
        OfflineAuthorityLease lease, DateTime threshold, CancellationToken cancellationToken)
    {
        var ids = new HashSet<long>(await ChangedSince(db.ProductVariants, threshold)
            .Select(x => x.Id).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.Products, threshold)
            .SelectMany(x => x.Variants).Select(x => x.Id).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.Barcodes, threshold)
            .Select(x => x.VariantId).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.Stocks, threshold)
            .Where(x => x.WarehouseId == lease.WarehouseId)
            .Select(x => x.VariantId).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.BranchCatalogEntries, threshold)
            .Where(x => x.BranchId == lease.BranchId)
            .Select(x => x.VariantId).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.ProductPrices, threshold)
            .Where(x => x.WarehouseId == lease.WarehouseId || x.WarehouseId == null)
            .Select(x => x.VariantId).ToListAsync(cancellationToken));
        return [.. ids];
    }

    /// Mijoz kesh qatorida qarz balansi ham bor — u mijoz qatoriga tegmasdan `accounts` orqali o'zgaradi.
    private async Task<long[]> ChangedCustomerIdsAsync(DateTime threshold, CancellationToken cancellationToken)
    {
        var ids = new HashSet<long>(await ChangedSince(db.Customers, threshold)
            .Select(x => x.Id).ToListAsync(cancellationToken));
        // OFF-53: ism va telefon Party'da yashaydi, ya'ni ularning tahriri customers qatoriga
        // tegmaydi — o'zgargan shaxsni ham deltaga qo'shmasak, qurilmada eski ism qolib ketardi.
        ids.UnionWith(await ChangedSince(db.Parties, threshold)
            .Where(x => x.CustomerProfile != null)
            .Select(x => x.CustomerProfile!.Id).ToListAsync(cancellationToken));
        ids.UnionWith(await ChangedSince(db.Accounts, threshold)
            .Where(x => x.CustomerId != null && x.Type == AccountType.Debt)
            .Select(x => x.CustomerId!.Value).ToListAsync(cancellationToken));
        return [.. ids];
    }

    private async Task<List<string>> RemovedBarcodeCodesAsync(
        DateTime threshold, long[] removedVariantIds, CancellationToken cancellationToken)
    {
        var codes = await ChangedSince(db.Barcodes, threshold)
            .Where(x => x.IsDeleted)
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        if (removedVariantIds.Length > 0)
            codes.AddRange(await db.Barcodes.AsNoTracking().IgnoreQueryFilters()
                .Where(x => removedVariantIds.Contains(x.VariantId))
                .Select(x => x.Code)
                .ToListAsync(cancellationToken));
        return [.. codes.Distinct()];
    }

    /// OFF-54(d): sanoq butun kesh bo'yicha — delta qatorlari emas.
    private async Task<OfflineSnapshotTotals> CacheTotalsAsync(
        OfflineAuthorityLease lease, SalesPolicySettings policy, bool includeProducts,
        bool includeCustomers, bool includeSuppliers, CancellationToken cancellationToken)
    {
        var productCount = 0;
        var barcodeCount = 0;
        if (includeProducts)
        {
            var forSale = await CatalogVisibility.ForSaleAsync(db.ProductVariants.AsNoTracking(),
                db, policy, lease.BranchId, lease.WarehouseId, cancellationToken);
            productCount = await forSale.CountAsync(cancellationToken);
            barcodeCount = await db.Barcodes.AsNoTracking()
                .CountAsync(b => forSale.Any(v => v.Id == b.VariantId), cancellationToken);
        }

        return new OfflineSnapshotTotals(
            productCount,
            barcodeCount,
            includeCustomers ? await db.Customers.CountAsync(cancellationToken) : 0,
            includeSuppliers ? await db.Suppliers.CountAsync(cancellationToken) : 0);
    }

    /// O'chirilgan qatorlar global filtrdan o'tmaydi, shuning uchun o'zgarish qidiruvi filtrsiz ishlaydi.
    private static IQueryable<T> ChangedSince<T>(IQueryable<T> source, DateTime threshold) where T : AuditableEntity =>
        source.AsNoTracking().IgnoreQueryFilters()
            .Where(x => (x.UpdatedAt ?? x.CreatedAt) > threshold);

    private static long[] Removed(IEnumerable<long> changed, IEnumerable<long> present)
    {
        var kept = present.ToHashSet();
        return [.. changed.Where(id => !kept.Contains(id))];
    }

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;

    private static DateTime? Utc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };
}
