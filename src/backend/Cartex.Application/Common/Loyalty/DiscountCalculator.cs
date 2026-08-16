using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Loyalty;

public record DiscountCalcLine(long VariantId, decimal LineTotal);

public interface IDiscountCalculator
{
    Task<List<DiscountApplication>> CalculateAsync(long? customerId, IReadOnlyCollection<DiscountCalcLine> lines, CancellationToken cancellationToken);
}

public sealed class DiscountCalculator(IApplicationDbContext db, IFeatureStateProvider features) : IDiscountCalculator
{
    public async Task<List<DiscountApplication>> CalculateAsync(long? customerId, IReadOnlyCollection<DiscountCalcLine> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0 || !await features.IsEnabledAsync(FeatureCatalog.Loyalty, cancellationToken))
            return [];

        var rules = await db.DiscountRules
            .Include(r => r.Exceptions)
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken);

        var customerPct = customerId is null
            ? 0
            : await db.Customers.Where(c => c.Id == customerId).Select(c => c.DiscountPct).FirstOrDefaultAsync(cancellationToken);

        if (rules.Count == 0 && customerPct <= 0)
            return [];

        var mode = await db.LoyaltyPrograms
            .Where(p => p.BranchId == null)
            .Select(p => p.DiscountCombineMode)
            .FirstOrDefaultAsync(cancellationToken);

        var variantIds = lines.Select(l => l.VariantId).Distinct().ToList();
        var products = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.Product.CategoryId, v.Product.ManufacturerId })
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        // A variant the catalogue no longer knows drops out here, which shifts every later index.
        // The caller allocates by position, so the engine's per-line split is mapped back onto the
        // lines it was handed rather than onto whatever survived the filter.
        var sourceIndexes = new List<int>(lines.Count);
        var discountLines = new List<DiscountLine>(lines.Count);
        var index = 0;
        foreach (var line in lines)
        {
            if (products.TryGetValue(line.VariantId, out var p))
            {
                sourceIndexes.Add(index);
                discountLines.Add(new DiscountLine(p.ProductId, p.CategoryId, p.ManufacturerId, line.LineTotal));
            }
            index++;
        }

        var applied = DiscountEngine.Compute(rules, customerPct, mode, customerId, DateOnly.FromDateTime(DateTime.Now), discountLines);
        if (discountLines.Count == lines.Count) return applied;

        return applied
            .Select(a => a.LineAmounts is { } shares ? a with { LineAmounts = Expand(shares, sourceIndexes, lines.Count) } : a)
            .ToList();
    }

    private static decimal[] Expand(IReadOnlyList<decimal> shares, List<int> sourceIndexes, int count)
    {
        var expanded = new decimal[count];
        for (var i = 0; i < shares.Count; i++) expanded[sourceIndexes[i]] = shares[i];
        return expanded;
    }
}
