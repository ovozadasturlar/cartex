using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Loyalty.Queries;

public record GetLoyaltyProgramQuery : IRequest<LoyaltyProgramDto>;

public record CashbackRuleDto(long Id, string Scope, long TargetId, string TargetName, string Method, decimal Value, int Priority, bool ExcludeFromTotalPercent);

public record LoyaltyProgramDto(bool IsEnabled, decimal TotalPercent, decimal CashbackRounding, List<CashbackRuleDto> Rules, string DiscountCombineMode = "Priority");

public sealed class GetLoyaltyProgramQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLoyaltyProgramQuery, LoyaltyProgramDto>
{
    public async Task<LoyaltyProgramDto> Handle(GetLoyaltyProgramQuery request, CancellationToken cancellationToken)
    {
        var program = await db.LoyaltyPrograms
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.BranchId == null, cancellationToken);

        if (program is null)
            return new LoyaltyProgramDto(false, 0, 0, []);

        var productIds = program.Rules.Where(r => r.Scope == CashbackScope.Product).Select(r => r.TargetId).ToList();
        var categoryIds = program.Rules.Where(r => r.Scope == CashbackScope.Category).Select(r => r.TargetId).ToList();

        var productNames = await db.Products.Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        var categoryNames = await db.Categories.Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var rules = program.Rules
            .OrderByDescending(r => r.Priority)
            .Select(r => new CashbackRuleDto(
                r.Id,
                r.Scope.ToString(),
                r.TargetId,
                r.Scope == CashbackScope.Product
                    ? productNames.GetValueOrDefault(r.TargetId, "")
                    : categoryNames.GetValueOrDefault(r.TargetId, ""),
                r.Method.ToString(),
                r.Value,
                r.Priority,
                r.ExcludeFromTotalPercent))
            .ToList();

        return new LoyaltyProgramDto(program.IsEnabled, program.TotalPercent, program.CashbackRounding, rules, program.DiscountCombineMode.ToString());
    }
}
