using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerTotalsQuery : FilteringRequest, IRequest<CustomerTotalsDto>;

public sealed class GetCustomerTotalsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCustomerTotalsQuery, CustomerTotalsDto>
{
    public async Task<CustomerTotalsDto> Handle(GetCustomerTotalsQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var customers = db.Customers.AsQueryable();
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AssignedUserId == currentUser.UserId);
        var query = customers.AsRows().AsFilterable(request);
        var count = await query.CountAsync(cancellationToken);
        var sums = await query
            .SelectMany(c => db.Accounts.Where(a => a.CustomerId == c.Id))
            .Where(a => a.Type == AccountType.Debt || a.Type == AccountType.Bonus || a.Type == AccountType.CustomerAdvance)
            .GroupBy(a => new { a.Type, a.Currency })
            .Select(g => new
            {
                g.Key.Type,
                g.Key.Currency,
                Sum = g.Sum(a => a.Balance),
                Owed = g.Sum(a => a.Balance > 0 ? a.Balance : 0m),
                Credit = g.Sum(a => a.Balance)
            })
            .ToListAsync(cancellationToken);
        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);
        decimal ToBase(string code, decimal amount) => amount * (code == baseCode ? 1m : rates.GetValueOrDefault(code));
        return new CustomerTotalsDto(
            count,
            sums.Where(s => s.Type == AccountType.Debt).Sum(s => ToBase(s.Currency, s.Owed)),
            sums.Where(s => s.Type == AccountType.Bonus).Sum(s => s.Sum),
            sums.Where(s => s.Type == AccountType.CustomerAdvance).Sum(s => ToBase(s.Currency, s.Credit)));
    }
}
