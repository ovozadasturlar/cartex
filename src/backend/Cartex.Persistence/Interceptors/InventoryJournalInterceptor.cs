using Cartex.Domain.Common;
using Cartex.Domain.Entities;
using Cartex.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

/// OMBOR-01: `stocks` miqdorini o'zgartiradigan har bir yo'l harakat qatorini yozadi. Yozish
/// saqlash nuqtasida bajariladi — handler buni alohida eslab qolishi shart bo'lmasin (`NARX-14`
/// dagi narx tarixi bilan bir xil yondashuv). OMBOR-03: sabab `InventoryReasonState` orqali
/// e'lon qilinadi; e'lon qilinmagan bo'lsa saqlash to'xtaydi. Sabab uni ishlatgan saqlashdan
/// keyin bekor qilinadi — keyingi e'lonsiz o'zgarish eskisini meros qilib olmaydi.
public sealed class InventoryJournalInterceptor(InventoryReasonState reasons, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private bool _consumed;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Release();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Release();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Release()
    {
        if (!_consumed) return;
        _consumed = false;
        reasons.Release();
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<InventoryMovement>())
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    "OMBOR-02: ombor harakati o'zgarmas — yozilgan qator tahrirlanmaydi va o'chirilmaydi.");

        var deltas = context.ChangeTracker.Entries<Stock>()
            .Select(entry => (entry.Entity, Delta: DeltaOf(entry)))
            .Where(x => x.Delta != 0)
            .ToList();
        if (deltas.Count == 0) return;

        var reason = reasons.Current ?? throw new InvalidOperationException(
            "OMBOR-03: ombor qoldig'i sababi e'lon qilinmasdan o'zgartirildi. "
            + $"Saqlashdan oldin {nameof(InventoryReasonState)}.{nameof(InventoryReasonState.Declare)} chaqirilsin.");

        _consumed = true;
        var now = DateTime.UtcNow;
        foreach (var (stock, delta) in deltas)
        {
            var inbound = delta > 0;
            var counterparty = inbound ? reason.Inbound : reason.Outbound;
            var warehouse = InventoryLocation.Warehouse(stock.WarehouseId);
            var from = inbound ? counterparty : warehouse;
            var to = inbound ? warehouse : counterparty;

            context.Set<InventoryMovement>().Add(new InventoryMovement
            {
                BranchId = stock.BranchId,
                WarehouseId = stock.WarehouseId,
                VariantId = stock.VariantId,
                Variant = stock.Variant,
                Stock = stock,
                Quantity = delta,
                Kind = reason.Kind,
                FromLocationKind = from.Kind,
                FromLocationId = from.Id,
                ToLocationKind = to.Kind,
                ToLocationId = to.Id,
                SourceType = reason.SourceType,
                SourceId = reason.SourceId,
                UserId = currentUser.UserId,
                OccurredAt = now
            });
        }
    }

    private static decimal DeltaOf(EntityEntry<Stock> entry)
    {
        var quantity = entry.Property(x => x.Quantity);
        if (entry.State == EntityState.Added)
            return entry.Entity.IsDeleted ? 0 : quantity.CurrentValue;
        if (entry.State != EntityState.Modified)
            return 0;

        var deleted = entry.Property(x => x.IsDeleted);
        if (deleted.OriginalValue)
            return 0;

        return deleted.CurrentValue ? -quantity.OriginalValue : quantity.CurrentValue - quantity.OriginalValue;
    }
}
