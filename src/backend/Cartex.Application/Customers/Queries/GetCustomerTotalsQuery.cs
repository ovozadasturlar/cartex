using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerTotalsQuery : FilteringRequest, IRequest<CustomerTotalsDto>;

public record CustomerTotalsDto(int Count, decimal TotalDebt, decimal TotalBonus);

public sealed class GetCustomerTotalsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCustomerTotalsQuery, CustomerTotalsDto>
{
    public async Task<CustomerTotalsDto> Handle(GetCustomerTotalsQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var query = db.Customers.AsFilterable(request);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            query = query.Where(c => c.AgentId == currentUser.UserId);
        var count = await query.CountAsync(cancellationToken);
        var sums = await query
            .SelectMany(c => c.Accounts)
            .Where(a => a.Type == AccountType.Debt || a.Type == AccountType.Bonus)
            .GroupBy(a => new { a.Type, a.Currency })
            .Select(g => new { g.Key.Type, g.Key.Currency, Sum = g.Sum(a => a.Balance) })
            .ToListAsync(cancellationToken);
        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);
        return new CustomerTotalsDto(
            count,
            sums.Where(s => s.Type == AccountType.Debt)
                .Sum(s => s.Sum * (s.Currency == baseCode ? 1m : rates.GetValueOrDefault(s.Currency))),
            sums.Where(s => s.Type == AccountType.Bonus).Sum(s => s.Sum));
    }
}
