using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cartex.Infrastructure.Catalog;

public sealed class ProductPopularityProvider(IApplicationDbContext db, IMemoryCache cache) : IProductPopularity
{
    private const int TopCount = 200;
    private static readonly TimeSpan Window = TimeSpan.FromDays(30);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(12);
    private static readonly IReadOnlyDictionary<long, int> Empty = new Dictionary<long, int>();

    public async Task<IReadOnlyDictionary<long, int>> GetRanksAsync(long branchId, CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"product-popularity:{branchId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheLifetime;
            var since = DateTime.UtcNow - Window;
            var ranked = await db.SaleItems
                .Where(item => item.Sale.BranchId == branchId
                    && item.Sale.Status != SaleStatus.Voided
                    && item.Sale.CreatedAt >= since)
                .GroupBy(item => item.Variant.ProductId)
                .Select(group => new { ProductId = group.Key, Sales = group.Select(item => item.SaleId).Distinct().Count() })
                .OrderByDescending(x => x.Sales)
                .ThenBy(x => x.ProductId)
                .Take(TopCount)
                .Select(x => x.ProductId)
                .ToListAsync(cancellationToken);
            return (IReadOnlyDictionary<long, int>)ranked
                .Select((productId, rank) => (productId, rank))
                .ToDictionary(x => x.productId, x => x.rank);
        }) ?? Empty;
}
