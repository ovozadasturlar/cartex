using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Loyalty.Queries;

public record DiscountExceptionDto(long ProductId, string ProductName);

public record DiscountRuleDto(long Id, string Name, bool IsEnabled, string Scope, long? TargetId, string? TargetName,
    long? CustomerId, string? CustomerName, decimal MinAmount, string Method, decimal Value, int Priority,
    DateOnly? StartsOn, DateOnly? EndsOn, List<DiscountExceptionDto> Exceptions);

public record GetDiscountRulesQuery : IRequest<IReadOnlyCollection<DiscountRuleDto>>;

public sealed class GetDiscountRulesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDiscountRulesQuery, IReadOnlyCollection<DiscountRuleDto>>
{
    public async Task<IReadOnlyCollection<DiscountRuleDto>> Handle(GetDiscountRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await db.DiscountRules
            .Include(r => r.Exceptions).ThenInclude(e => e.Product)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.Priority).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var productIds = rules.Where(r => r.Scope == DiscountScope.Product && r.TargetId != null).Select(r => r.TargetId!.Value).ToList();
        var categoryIds = rules.Where(r => r.Scope == DiscountScope.Category && r.TargetId != null).Select(r => r.TargetId!.Value).ToList();
        var manufacturerIds = rules.Where(r => r.Scope == DiscountScope.Manufacturer && r.TargetId != null).Select(r => r.TargetId!.Value).ToList();

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
                r.Exceptions.Select(e => new DiscountExceptionDto(e.ProductId, e.Product.Name)).ToList()))
            .ToList();
    }
}
