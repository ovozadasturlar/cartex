using Cartex.Domain.Common;
using Cartex.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

public sealed class BranchStampingInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
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

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var scopedToBranches = currentUser.IsAuthenticated && !currentUser.CanAccessAllBranches;

        foreach (var entry in context.ChangeTracker.Entries<IBranchScoped>())
        {
            if (entry.State != EntityState.Added) continue;

            var entity = entry.Entity;

            if (entity.BranchId == 0)
                entity.BranchId = currentUser.DefaultBranchId
                    ?? (currentUser.BranchIds.Count == 1 ? currentUser.BranchIds.First() : 0);

            if (scopedToBranches && !currentUser.BranchIds.Contains(entity.BranchId))
                throw new ForbiddenException("Cannot write data outside your branch.");
        }
    }
}
