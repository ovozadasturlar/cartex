using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerTotalsQuery : FilteringRequest, IRequest<CustomerTotalsDto>;

public record CustomerTotalsDto(int Count, decimal TotalDebt, decimal TotalBonus);

public sealed class GetCustomerTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCustomerTotalsQuery, CustomerTotalsDto>
{
    public async Task<CustomerTotalsDto> Handle(GetCustomerTotalsQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var query = db.Customers.AsFilterable(request);
        return new CustomerTotalsDto(
            await query.CountAsync(cancellationToken),
            await query.SumAsync(c => (decimal?)c.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())), cancellationToken) ?? 0,
            await query.SumAsync(c => (decimal?)c.Accounts.Where(a => a.Type == AccountType.Bonus).Sum(a => a.Balance), cancellationToken) ?? 0);
    }
}
