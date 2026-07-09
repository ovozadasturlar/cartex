using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
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

        var discountLines = lines
            .Where(l => products.ContainsKey(l.VariantId))
            .Select(l =>
            {
                var p = products[l.VariantId];
                return new DiscountLine(p.ProductId, p.CategoryId, p.ManufacturerId, l.LineTotal);
            })
            .ToList();

        return DiscountEngine.Compute(rules, customerPct, mode, customerId, DateOnly.FromDateTime(DateTime.Now), discountLines);
    }
}
