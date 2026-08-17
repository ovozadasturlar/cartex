using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Loyalty;

namespace Cartex.Application.Loyalty.Queries;

public record GetLoyaltyStatsQuery(DateTime FromDate, DateTime ToDate) : IRequest<LoyaltyStatsDto>;

public sealed class GetLoyaltyStatsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLoyaltyStatsQuery, LoyaltyStatsDto>
{
    public async Task<LoyaltyStatsDto> Handle(GetLoyaltyStatsQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.SpecifyKind(request.FromDate, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(request.ToDate, DateTimeKind.Utc);

        var agg = await db.Sales
            .Where(s => s.Status != SaleStatus.Voided && s.CreatedAt >= from && s.CreatedAt < to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                SalesCount = g.Count(),
                Discounted = g.Count(s => s.DiscountAmount > 0),
                DiscountTotal = g.Sum(s => s.DiscountAmount),
                GrossTotal = g.Sum(s => s.TotalAmount + s.DiscountAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var bonusOutstanding = await db.Accounts
            .Where(a => a.Type == AccountType.Bonus && a.CustomerId != null)
            .SumAsync(a => a.Balance, cancellationToken);

        return new LoyaltyStatsDto(agg?.SalesCount ?? 0, agg?.Discounted ?? 0, agg?.DiscountTotal ?? 0, agg?.GrossTotal ?? 0, bonusOutstanding);
    }
}
