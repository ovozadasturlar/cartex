using Cartex.Domain.Entities;
using Cartex.Shared.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cartex.Persistence.Interceptors;

public sealed class SearchFoldInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Apply(DbContext? context)
    {
        if (context is null)
            return;

        Apply(context.ChangeTracker.Entries<Product>(), nameof(Product.Name), x => x.Name, (x, value) => x.SearchFold = value);
        Apply(context.ChangeTracker.Entries<ProductReference>(), nameof(ProductReference.Name), x => x.Name, (x, value) => x.SearchFold = value);
        Apply(context.ChangeTracker.Entries<Customer>(), nameof(Customer.FullName), x => x.FullName, (x, value) => x.SearchFold = value);
        Apply(context.ChangeTracker.Entries<Supplier>(), nameof(Supplier.Name), x => x.Name, (x, value) => x.SearchFold = value);
        Apply(context.ChangeTracker.Entries<Category>(), nameof(Category.Name), x => x.Name, (x, value) => x.SearchFold = value);
        Apply(context.ChangeTracker.Entries<Manufacturer>(), nameof(Manufacturer.Name), x => x.Name, (x, value) => x.SearchFold = value);
    }

    private static void Apply<TEntity>(
        IEnumerable<EntityEntry<TEntity>> entries,
        string nameProperty,
        Func<TEntity, string?> getName,
        Action<TEntity, string> setFold) where TEntity : class
    {
        foreach (var entry in entries)
        {
            if (entry.State != EntityState.Added
                && (entry.State != EntityState.Modified || !entry.Property(nameProperty).IsModified))
                continue;

            setFold(entry.Entity, SearchFold.Fuzzy(getName(entry.Entity)));
        }
    }
}
