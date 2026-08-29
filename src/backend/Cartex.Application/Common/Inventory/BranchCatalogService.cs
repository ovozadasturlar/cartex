using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Inventory;

public interface IBranchCatalogService
{
    Task ActivateAsync(long branchId, IEnumerable<long> variantIds, BranchCatalogActivationSource source, CancellationToken cancellationToken);
}

public sealed class BranchCatalogService(IApplicationDbContext db) : IBranchCatalogService
{
    public async Task ActivateAsync(long branchId, IEnumerable<long> variantIds, BranchCatalogActivationSource source, CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().ToArray();
        if (ids.Length == 0)
            return;

        var entries = await db.BranchCatalogEntries
            .IgnoreQueryFilters()
            .Where(x => x.BranchId == branchId && ids.Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var variantId in ids)
        {
            if (!entries.TryGetValue(variantId, out var entry))
            {
                db.BranchCatalogEntries.Add(new BranchCatalogEntry
                {
                    BranchId = branchId,
                    VariantId = variantId,
                    FirstActivityAt = now,
                    LastActivityAt = now,
                    ActivationSource = source
                });
                continue;
            }

            entry.IsDeleted = false;
            entry.DeletedAt = null;
            entry.FirstActivityAt ??= now;
            entry.LastActivityAt = now;
            entry.ActivationSource = source;
        }
    }
}
