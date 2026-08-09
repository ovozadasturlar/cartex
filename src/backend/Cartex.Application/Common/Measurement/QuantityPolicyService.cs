using Cartex.Domain.Common.Exceptions;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Measurement;

public sealed record EffectiveQuantityPolicy(decimal Step, bool AllowsAmountEntry)
{
    public bool AllowsFractional => Step != decimal.Truncate(Step);
}

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
    public const decimal MinimumStep = 0.001m;
    public const decimal MaximumStep = 1_000_000m;

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
                    Step = v.Product.QuantityStepOverride ?? v.Product.Unit.DefaultQuantityStep,
                    AllowsAmountEntry = v.Product.AmountEntryEnabled ?? v.Product.Unit.DefaultAllowAmountEntry
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
                _cache[row.Id] = new EffectiveQuantityPolicy(row.Step, row.AllowsAmountEntry);
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
            if (!IsValid(quantity, policy.Step, allowZero))
                throw new BusinessRuleException(
                    $"Miqdor {policy.Step:0.###} qadamiga mos bo'lishi kerak.",
                    "quantity_step_violation");
        }
    }

    public static bool IsValid(decimal quantity, decimal step, bool allowZero = false) =>
        (quantity > 0 || allowZero && quantity == 0) &&
        step is >= MinimumStep and <= MaximumStep &&
        quantity == decimal.Round(quantity, 3) &&
        quantity % step == 0;
}
