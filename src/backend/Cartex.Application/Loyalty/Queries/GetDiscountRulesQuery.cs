using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Loyalty;

namespace Cartex.Application.Loyalty.Queries;

public record GetDiscountRulesQuery : IRequest<IReadOnlyCollection<DiscountRuleDto>>;

public sealed class GetDiscountRulesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDiscountRulesQuery, IReadOnlyCollection<DiscountRuleDto>>
{
    public async Task<IReadOnlyCollection<DiscountRuleDto>> Handle(GetDiscountRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.DiscountRules
            .Include(r => r.Exceptions)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.Priority).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        List<long> IdsOf(DiscountScope scope) =>
            rules.Where(r => r.Scope == scope && r.TargetId != null).Select(r => r.TargetId!.Value)
                .Concat(rules.SelectMany(r => r.Exceptions).Where(e => e.Scope == scope).Select(e => e.TargetId))
                .Distinct().ToList();

        var productIds = IdsOf(DiscountScope.Product);
        var categoryIds = IdsOf(DiscountScope.Category);
        var manufacturerIds = IdsOf(DiscountScope.Manufacturer);

        var productNames = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        var categoryNames = await db.Categories.Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var manufacturerNames = await db.Manufacturers.Where(m => manufacturerIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        return rules.Select(r => new DiscountRuleDto(
                r.Id, r.Name, r.IsEnabled, r.Scope.ToString(), r.TargetId,
                r.TargetId is null ? null : r.Scope switch
                {
                    DiscountScope.Product => productNames.GetValueOrDefault(r.TargetId.Value),
                    DiscountScope.Category => categoryNames.GetValueOrDefault(r.TargetId.Value),
                    DiscountScope.Manufacturer => manufacturerNames.GetValueOrDefault(r.TargetId.Value),
                    _ => null
                },
                r.CustomerId, r.Customer?.FullName, r.MinAmount, r.Method.ToString(), r.Value, r.Priority,
                r.StartsOn, r.EndsOn,
                r.Exceptions.Select(e => new DiscountExceptionDto(e.Scope.ToString(), e.TargetId, e.Scope switch
                {
                    DiscountScope.Product => productNames.GetValueOrDefault(e.TargetId) ?? "",
                    DiscountScope.Category => categoryNames.GetValueOrDefault(e.TargetId) ?? "",
                    _ => manufacturerNames.GetValueOrDefault(e.TargetId) ?? ""
                })).ToList()))
            .ToList();
    }
}
