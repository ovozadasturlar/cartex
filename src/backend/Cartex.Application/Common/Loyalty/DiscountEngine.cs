using Cartex.Domain.Entities;
using Cartex.Domain.Enums;

namespace Cartex.Application.Common.Loyalty;

public record DiscountLine(long ProductId, long? CategoryId, long? ManufacturerId, decimal LineTotal);

public record DiscountApplication(string Name, decimal Amount);

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
        var subtotal = lines.Sum(l => l.LineTotal);
        if (subtotal <= 0) return [];

        var candidates = new List<(int Priority, DiscountApplication App)>();

        foreach (var rule in rules)
        {
            if (!rule.IsEnabled) continue;
            if (rule.CustomerId is not null && rule.CustomerId != customerId) continue;
            if (rule.StartsOn is { } from && today < from) continue;
            if (rule.EndsOn is { } to && today > to) continue;

            var matched = lines
                .Where(l => Matches(rule, l) && !IsExcluded(rule, l))
                .Sum(l => l.LineTotal);

            if (matched <= 0) continue;
            if (rule.MinAmount > 0 && matched < rule.MinAmount) continue;

            var amount = rule.Method == DiscountMethod.Percent
                ? matched * rule.Value / 100
                : Math.Min(rule.Value, matched);
            if (amount <= 0) continue;

            candidates.Add((rule.Priority, new DiscountApplication(rule.Name, Math.Round(amount, 2))));
        }

        if (customerPct > 0)
            candidates.Add((int.MaxValue, new DiscountApplication("customer", Math.Round(subtotal * customerPct / 100, 2))));

        if (candidates.Count == 0) return [];

        var applied = mode == DiscountCombineMode.Stack
            ? candidates.Select(c => c.App).ToList()
            : [candidates.OrderByDescending(c => c.Priority).ThenByDescending(c => c.App.Amount).First().App];

        var total = applied.Sum(a => a.Amount);
        if (total > subtotal && total > 0)
        {
            var factor = subtotal / total;
            applied = applied.Select(a => a with { Amount = Math.Round(a.Amount * factor, 2) }).ToList();
        }
        return applied;
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
