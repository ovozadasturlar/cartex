using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Loyalty;

public record CashbackLine(long ProductId, decimal Quantity, decimal LineTotal);
public record CashbackCalculation(decimal Total, IReadOnlyList<decimal> LineAmounts);

public interface ICashbackCalculator
{
    Task<decimal> CalculateAsync(long branchId, IReadOnlyCollection<CashbackLine> lines, CancellationToken cancellationToken);
    Task<CashbackCalculation> CalculateBreakdownAsync(long branchId, IReadOnlyList<CashbackLine> lines, CancellationToken cancellationToken);
}

public interface ICashbackStrategy
{
    CashbackMethod Method { get; }
    decimal Calculate(CashbackLine line, decimal value);
}

public sealed class PercentCashbackStrategy : ICashbackStrategy
{
    public CashbackMethod Method => CashbackMethod.Percent;
    public decimal Calculate(CashbackLine line, decimal value) => line.LineTotal * value / 100;
}

public sealed class FixedPerUnitCashbackStrategy : ICashbackStrategy
{
    public CashbackMethod Method => CashbackMethod.FixedPerUnit;
    public decimal Calculate(CashbackLine line, decimal value) => line.Quantity * value;
}

public sealed class CashbackCalculator(IApplicationDbContext db, IEnumerable<ICashbackStrategy> strategies) : ICashbackCalculator
{
    public async Task<decimal> CalculateAsync(long branchId, IReadOnlyCollection<CashbackLine> lines, CancellationToken cancellationToken) =>
        (await CalculateBreakdownAsync(branchId, lines.ToList(), cancellationToken)).Total;

    public async Task<CashbackCalculation> CalculateBreakdownAsync(
        long branchId,
        IReadOnlyList<CashbackLine> lines,
        CancellationToken cancellationToken)
    {
        var programs = await db.LoyaltyPrograms
            .Where(p => p.IsEnabled && (p.BranchId == branchId || p.BranchId == null))
            .Include(p => p.Rules)
            .ToListAsync(cancellationToken);

        var program = programs.FirstOrDefault(p => p.BranchId == branchId)
            ?? programs.FirstOrDefault(p => p.BranchId == null);

        if (program is null)
            return new CashbackCalculation(0, lines.Select(_ => 0m).ToList());

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var categoryByProduct = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CategoryId })
            .ToDictionaryAsync(p => p.Id, p => p.CategoryId, cancellationToken);

        var rawByLine = new List<decimal>(lines.Count);
        foreach (var line in lines)
        {
            var rule = ResolveRule(program, line.ProductId, categoryByProduct.GetValueOrDefault(line.ProductId));
            var strategy = rule is null ? null : strategies.FirstOrDefault(s => s.Method == rule.Method);
            decimal lineReward = 0;
            if (rule is not null && strategy is not null)
            {
                lineReward += strategy.Calculate(line, rule.Value);
                if (!rule.ExcludeFromTotalPercent)
                    lineReward += line.LineTotal * program.TotalPercent / 100;
            }
            else
            {
                lineReward += line.LineTotal * program.TotalPercent / 100;
            }
            rawByLine.Add(lineReward);
        }

        var raw = rawByLine.Sum();
        var total = program.CashbackRounding > 0
            ? Math.Floor(raw / program.CashbackRounding) * program.CashbackRounding
            : raw;
        total = Math.Round(total, 2);
        if (raw <= 0 || total <= 0)
            return new CashbackCalculation(0, lines.Select(_ => 0m).ToList());

        var allocated = new List<decimal>(lines.Count);
        decimal used = 0;
        for (var i = 0; i < rawByLine.Count; i++)
        {
            var amount = i == rawByLine.Count - 1
                ? total - used
                : Math.Round(total * rawByLine[i] / raw, 2);
            allocated.Add(amount);
            used += amount;
        }
        return new CashbackCalculation(total, allocated);
    }

    private static CashbackRule? ResolveRule(LoyaltyProgram program, long productId, long? categoryId)
    {
        var productRule = program.Rules
            .Where(r => r.Scope == CashbackScope.Product && r.TargetId == productId)
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault();

        if (productRule is not null)
            return productRule;

        if (categoryId is null)
            return null;

        return program.Rules
            .Where(r => r.Scope == CashbackScope.Category && r.TargetId == categoryId.Value)
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault();
    }
}
