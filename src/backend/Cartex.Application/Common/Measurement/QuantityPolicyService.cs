using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Measurement;

public sealed record EffectiveQuantityPolicy(bool AllowsFractional, bool AllowsAmountEntry);

public interface IQuantityPolicyService
{
    Task<IReadOnlyDictionary<long, EffectiveQuantityPolicy>> ResolveAsync(
        IEnumerable<long> variantIds,
        CancellationToken cancellationToken);

    Task ValidateAsync(
        IEnumerable<(long VariantId, decimal Quantity)> lines,
        CancellationToken cancellationToken,
        bool allowZero = false);
}

public sealed class QuantityPolicyService(IApplicationDbContext db) : IQuantityPolicyService
{
    private readonly Dictionary<long, EffectiveQuantityPolicy> _cache = [];

    public async Task<IReadOnlyDictionary<long, EffectiveQuantityPolicy>> ResolveAsync(
        IEnumerable<long> variantIds,
        CancellationToken cancellationToken)
    {
        var ids = variantIds.Distinct().ToArray();
        var missingIds = ids.Where(id => !_cache.ContainsKey(id)).ToArray();
        if (missingIds.Length > 0)
        {
            var rows = await db.ProductVariants
                .Where(v => missingIds.Contains(v.Id))
                .Select(v => new
                {
                    v.Id,
                    AllowsFractional = v.Product.FractionalOverride ?? v.Product.Unit.AllowFractional,
                    AllowsAmountEntry = v.Product.AmountEntryEnabled ?? v.Product.Unit.DefaultAllowAmountEntry
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                _cache[row.Id] = new EffectiveQuantityPolicy(row.AllowsFractional, row.AllowsAmountEntry);
        }

        var unresolved = ids.Where(id => !_cache.ContainsKey(id)).Order().ToArray();
        if (unresolved.Length > 0)
            throw new NotFoundException($"Product variant not found: {string.Join(", ", unresolved)}.", "variant_not_found");

        return ids.ToDictionary(id => id, id => _cache[id]);
    }

    public async Task ValidateAsync(
        IEnumerable<(long VariantId, decimal Quantity)> lines,
        CancellationToken cancellationToken,
        bool allowZero = false)
    {
        var materialized = lines.ToArray();
        var policies = await ResolveAsync(materialized.Select(x => x.VariantId), cancellationToken);

        foreach (var (variantId, quantity) in materialized)
        {
            var policy = policies[variantId];
            EnsureValid(quantity, policy.AllowsFractional, allowZero);
        }
    }

    public static void EnsureValid(decimal quantity, bool allowFractional, bool allowZero = false, string messagePrefix = "")
    {
        if ((quantity > 0 || allowZero && quantity == 0) && quantity != decimal.Round(quantity, 3))
            throw new BusinessRuleException($"{messagePrefix}Miqdor 0.001 aniqlikdan oshmaydi.", "quantity_precision_exceeded");
        if (!allowFractional && quantity != decimal.Truncate(quantity))
            throw new BusinessRuleException($"{messagePrefix}Miqdor faqat butun son bo'lishi kerak.", "quantity_whole_required");
        if (!IsValid(quantity, allowFractional, allowZero))
            throw new BusinessRuleException($"{messagePrefix}Miqdor musbat bo'lishi kerak.", "quantity_positive_required");
    }

    public static bool IsValid(decimal quantity, bool allowFractional, bool allowZero = false) =>
        (quantity > 0 || allowZero && quantity == 0) &&
        quantity == decimal.Round(quantity, 3) &&
        (allowFractional || quantity == decimal.Truncate(quantity));
}
