using Cartex.Persistence;
using Cartex.Shared.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Search;

public sealed class SearchFoldBackfillService(
    IServiceScopeFactory scopeFactory,
    ILogger<SearchFoldBackfillService> logger) : BackgroundService
{
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await FillAsync(db.Products.Where(x => x.SearchFold == null), x => x.Name, (x, value) => x.SearchFold = value, db, stoppingToken);
            await FillAsync(db.Customers.Where(x => x.SearchFold == null), x => x.FullName, (x, value) => x.SearchFold = value, db, stoppingToken);
            await FillAsync(db.Suppliers.Where(x => x.SearchFold == null), x => x.Name, (x, value) => x.SearchFold = value, db, stoppingToken);
            await FillAsync(db.Categories.Where(x => x.SearchFold == null), x => x.Name, (x, value) => x.SearchFold = value, db, stoppingToken);
            await FillAsync(db.Manufacturers.Where(x => x.SearchFold == null), x => x.Name, (x, value) => x.SearchFold = value, db, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Search fold backfill was cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Search fold backfill failed");
        }
    }

    private static async Task FillAsync<TEntity>(
        IQueryable<TEntity> query,
        Func<TEntity, string?> getName,
        Action<TEntity, string> setFold,
        ApplicationDbContext db,
        CancellationToken cancellationToken) where TEntity : class
    {
        while (true)
        {
            var rows = await query.Take(BatchSize).ToListAsync(cancellationToken);
            if (rows.Count == 0)
                return;

            foreach (var row in rows)
                setFold(row, SearchFold.Fuzzy(getName(row)));

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }
}
