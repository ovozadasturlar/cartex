using Cartex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

/// NARX-14: katalog narxi o'zgarganda tugagan versiya tarixga tushadi. Bu saqlash nuqtasida
/// bajariladi — narxni o'zgartiradigan har bir yo'l (qo'lda tahrir, import, savdodan yangilash)
/// buni alohida eslab qolishi shart bo'lmasin.
public sealed class PriceHistoryInterceptor : SaveChangesInterceptor
{
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

    private static void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var rows = new List<ProductPriceHistory>();

        foreach (var entry in context.ChangeTracker.Entries<ProductPrice>())
        {
            if (entry.State != EntityState.Modified) continue;

            var price = entry.Property(x => x.SellingPrice);
            var currency = entry.Property(x => x.Currency);
            if (!price.IsModified && !currency.IsModified) continue;
            if (price.OriginalValue == price.CurrentValue && currency.OriginalValue == currency.CurrentValue) continue;

            // Amal qilish boshlanishi — narx oxirgi marta yozilgan vaqt. Audit maydonlarining
            // asl qiymati olinadi: ularni boshqa interceptor allaqachon yangilagan bo'lishi mumkin.
            var updatedAt = entry.Property(x => x.UpdatedAt).OriginalValue;
            var createdAt = entry.Property(x => x.CreatedAt).OriginalValue;
            // NARX-14: oynalar tutash bo'lishi uchun tugash vaqti ham keyingi oynaning boshi bilan
            // bir xil muhrdan olinadi — audit interceptor'i qo'ygan yangi `UpdatedAt`.
            var endsAt = entry.Entity.UpdatedAt ?? now;

            rows.Add(new ProductPriceHistory
            {
                VariantId = entry.Entity.VariantId,
                WarehouseId = entry.Entity.WarehouseId,
                SellingPrice = price.OriginalValue,
                Currency = currency.OriginalValue,
                EffectiveFrom = updatedAt ?? createdAt,
                EffectiveTo = endsAt
            });
        }

        if (rows.Count > 0) context.Set<ProductPriceHistory>().AddRange(rows);
    }
}
