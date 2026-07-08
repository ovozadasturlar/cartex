using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Common.Loyalty;

public record CashbackLine(long ProductId, decimal Quantity, decimal LineTotal);

public interface ICashbackCalculator
{
    Task<decimal> CalculateAsync(long branchId, IReadOnlyCollection<CashbackLine> lines, CancellationToken cancellationToken);
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
    public async Task<decimal> CalculateAsync(long branchId, IReadOnlyCollection<CashbackLine> lines, CancellationToken cancellationToken)
    {
        var programs = await db.LoyaltyPrograms
            .Where(p => p.IsEnabled && (p.BranchId == branchId || p.BranchId == null))
            .Include(p => p.Rules)
            .ToListAsync(cancellationToken);

        var program = programs.FirstOrDefault(p => p.BranchId == branchId)
            ?? programs.FirstOrDefault(p => p.BranchId == null);

        if (program is null)
            return 0;

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var categoryByProduct = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CategoryId })
            .ToDictionaryAsync(p => p.Id, p => p.CategoryId, cancellationToken);

        decimal total = 0;
        decimal percentBase = 0;
        foreach (var line in lines)
        {
            var rule = ResolveRule(program, line.ProductId, categoryByProduct.GetValueOrDefault(line.ProductId));
            var strategy = rule is null ? null : strategies.FirstOrDefault(s => s.Method == rule.Method);
            if (rule is not null && strategy is not null)
            {
                total += strategy.Calculate(line, rule.Value);
                if (!rule.ExcludeFromTotalPercent)
                    percentBase += line.LineTotal;
            }
            else
            {
                percentBase += line.LineTotal;
            }
        }

        var raw = total + percentBase * program.TotalPercent / 100;
        return program.CashbackRounding > 0 ? Math.Floor(raw / program.CashbackRounding) * program.CashbackRounding : raw;
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
