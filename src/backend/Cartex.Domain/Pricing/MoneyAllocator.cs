using Cartex.Domain.Common.Exceptions;

namespace Cartex.Domain.Pricing;

public readonly record struct MoneyAllocation(IReadOnlyList<decimal> Placed, decimal Residual);

/// Splits a money amount across lines so the parts always add back up to the whole.
/// Each line is capped at its own capacity, so a discount can never drive a line below zero;
/// whatever cannot be placed comes back as the residual for the caller to subtract.
public static class MoneyAllocator
{
    public static MoneyAllocation Distribute(
        decimal amount, IReadOnlyList<decimal> weights, IReadOnlyList<decimal> capacities, int decimals = 2)
    {
        if (weights.Count != capacities.Count)
            throw new BusinessRuleException("Taqsimot vaznlari va sig'imlari soni mos kelmaydi.");
        if (amount < 0)
            throw new BusinessRuleException("Taqsimlanadigan summa manfiy bo'la olmaydi.");

        var step = Step(decimals);
        var placed = new decimal[weights.Count];
        var remaining = Math.Round(amount, decimals);

        while (remaining >= step)
        {
            var open = new List<int>(placed.Length);
            for (var i = 0; i < placed.Length; i++)
                if (Headroom(capacities[i], placed[i], decimals) >= step)
                    open.Add(i);
            if (open.Count == 0) break;

            var shares = Shares(open, weights, capacities, placed, remaining, step, decimals);

            var progressed = false;
            for (var k = 0; k < open.Count && remaining >= step; k++)
            {
                var i = open[k];
                var give = Math.Min(Math.Min(shares[k], Headroom(capacities[i], placed[i], decimals)), remaining);
                if (give < step) continue;
                placed[i] += give;
                remaining -= give;
                progressed = true;
            }
            if (!progressed) break;
        }

        return new MoneyAllocation(placed, remaining);
    }

    /// Largest remainder: every line gets its floored share, then the leftover units go to the
    /// lines that were rounded down hardest, so the shares sum to the amount exactly.
    private static decimal[] Shares(List<int> open, IReadOnlyList<decimal> weights, IReadOnlyList<decimal> capacities,
        decimal[] placed, decimal remaining, decimal step, int decimals)
    {
        var basis = new decimal[open.Count];
        decimal total = 0;
        for (var k = 0; k < open.Count; k++)
        {
            basis[k] = Math.Max(0, weights[open[k]]);
            total += basis[k];
        }

        if (total <= 0)
            for (var k = 0; k < open.Count; k++)
            {
                basis[k] = Headroom(capacities[open[k]], placed[open[k]], decimals);
                total += basis[k];
            }

        var shares = new decimal[open.Count];
        if (total <= 0) return shares;

        var units = decimal.Round(remaining / step, 0, MidpointRounding.AwayFromZero);
        var fractions = new (int Index, decimal Fraction)[open.Count];
        decimal assigned = 0;
        for (var k = 0; k < open.Count; k++)
        {
            var exact = units * basis[k] / total;
            var whole = decimal.Floor(exact);
            shares[k] = whole * step;
            assigned += whole;
            fractions[k] = (k, exact - whole);
        }

        var leftover = (int)Math.Max(0, units - assigned);
        foreach (var (index, _) in fractions.OrderByDescending(x => x.Fraction).ThenBy(x => x.Index).Take(leftover))
            shares[index] += step;

        return shares;
    }

    private static decimal Headroom(decimal capacity, decimal placed, int decimals) =>
        Math.Round(Math.Max(0, capacity - placed), decimals);

    private static decimal Step(int decimals)
    {
        if (decimals is < 0 or > 8)
            throw new BusinessRuleException("Yaxlitlash aniqligi 0 va 8 orasida bo'lishi kerak.");
        var step = 1m;
        for (var i = 0; i < decimals; i++) step /= 10m;
        return step;
    }
}
