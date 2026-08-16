using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Domain.Pricing;

namespace Cartex.Application.Common.Loyalty;

public record DiscountLine(long ProductId, long? CategoryId, long? ManufacturerId, decimal LineTotal);

/// LineAmounts says how much of Amount belongs to each input line, in the caller's own order.
/// A rule scoped to one product must not make the rest of the basket cheaper, so the split the
/// engine already knows is handed back instead of being re-derived from the order total.
public record DiscountApplication(string Name, decimal Amount, IReadOnlyList<decimal>? LineAmounts = null);

public static class DiscountEngine
{
    public static List<DiscountApplication> Compute(
        IReadOnlyCollection<DiscountRule> rules,
        decimal customerPct,
        DiscountCombineMode mode,
        long? customerId,
        DateOnly today,
        IReadOnlyCollection<DiscountLine> lines)
    {
        var basket = lines as IReadOnlyList<DiscountLine> ?? [.. lines];
        var subtotal = basket.Sum(l => l.LineTotal);
        if (subtotal <= 0) return [];

        var candidates = new List<(int Priority, DiscountApplication App)>();

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled) continue;
            if (rule.CustomerId is not null && rule.CustomerId != customerId) continue;
            if (rule.StartsOn is { } from && today < from) continue;
            if (rule.EndsOn is { } to && today > to) continue;

            var weights = new decimal[basket.Count];
            decimal matched = 0;
            for (var i = 0; i < basket.Count; i++)
            {
                if (!Matches(rule, basket[i]) || IsExcluded(rule, basket[i])) continue;
                weights[i] = Math.Max(0, basket[i].LineTotal);
                matched += basket[i].LineTotal;
            }

            if (matched <= 0) continue;
            if (rule.MinAmount > 0 && matched < rule.MinAmount) continue;

            var amount = rule.Method == DiscountMethod.Percent
                ? matched * rule.Value / 100
                : Math.Min(rule.Value, matched);
            if (amount <= 0) continue;

            candidates.Add((rule.Priority, Split(rule.Name, Math.Round(amount, 2), weights)));
        }

        if (customerPct > 0)
            candidates.Add((int.MaxValue, Split("customer", Math.Round(subtotal * customerPct / 100, 2),
                [.. basket.Select(l => Math.Max(0, l.LineTotal))])));

        if (candidates.Count == 0) return [];

        var applied = mode == DiscountCombineMode.Stack
            ? candidates.Select(c => c.App).ToList()
            : [candidates.OrderByDescending(c => c.Priority).ThenByDescending(c => c.App.Amount).First().App];

        var total = applied.Sum(a => a.Amount);
        if (total > subtotal && total > 0)
        {
            var factor = subtotal / total;
            applied = applied
                .Select(a => Split(a.Name, Math.Round(a.Amount * factor, 2), a.LineAmounts!))
                .ToList();
        }
        return applied;
    }

    /// Each line is capped at its own value, so a rule can never take more off a line than the
    /// line is worth; whatever will not fit is dropped from the application total as well.
    private static DiscountApplication Split(string name, decimal amount, IReadOnlyList<decimal> weights)
    {
        var placement = MoneyAllocator.Distribute(amount, weights, weights);
        return new DiscountApplication(name, amount - placement.Residual, placement.Placed);
    }

    public static decimal? BestPercent(
        IReadOnlyCollection<DiscountRule> rules,
        DateOnly today,
        long productId,
        long? categoryId,
        long? manufacturerId)
    {
        var line = new DiscountLine(productId, categoryId, manufacturerId, 0);

        var best = rules
            .Where(r => r.IsEnabled && r.CustomerId is null && r.MinAmount == 0 && r.Method == DiscountMethod.Percent)
            .Where(r => r.StartsOn is not { } from || today >= from)
            .Where(r => r.EndsOn is not { } to || today <= to)
            .Where(r => Matches(r, line) && !IsExcluded(r, line))
            .Select(r => r.Value)
            .DefaultIfEmpty(0)
            .Max();

        return best > 0 ? best : null;
    }

    private static bool Matches(DiscountRule rule, DiscountLine line) => rule.Scope switch
    {
        DiscountScope.All => true,
        DiscountScope.Product => rule.TargetId == line.ProductId,
        DiscountScope.Category => rule.TargetId is not null && rule.TargetId == line.CategoryId,
        DiscountScope.Manufacturer => rule.TargetId is not null && rule.TargetId == line.ManufacturerId,
        _ => false
    };

    private static bool IsExcluded(DiscountRule rule, DiscountLine line) =>
        rule.Exceptions.Any(e => e.Scope switch
        {
            DiscountScope.Product => e.TargetId == line.ProductId,
            DiscountScope.Category => e.TargetId == line.CategoryId,
            DiscountScope.Manufacturer => e.TargetId == line.ManufacturerId,
            _ => false
        });
}
