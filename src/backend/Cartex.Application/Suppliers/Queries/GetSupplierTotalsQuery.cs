using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Suppliers;

namespace Cartex.Application.Suppliers.Queries;

public record GetSupplierTotalsQuery : FilteringRequest, IRequest<SupplierTotalsDto>;

public sealed class GetSupplierTotalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSupplierTotalsQuery, SupplierTotalsDto>
{
    public async Task<SupplierTotalsDto> Handle(GetSupplierTotalsQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var query = db.Suppliers.AsFilterable(request);
        var count = await query.CountAsync(cancellationToken);
        var sums = await query
            .SelectMany(s => s.Accounts)
            .Where(a => a.Type == AccountType.Debt)
            .GroupBy(a => a.Currency)
            .Select(g => new
            {
                Currency = g.Key,
                Payable = g.Sum(a => a.Balance < 0 ? -a.Balance : 0m),
                Advance = g.Sum(a => a.Balance > 0 ? a.Balance : 0m)
            })
            .ToListAsync(cancellationToken);
        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);
        decimal ToBase(string code, decimal amount) => amount * (code == baseCode ? 1m : rates.GetValueOrDefault(code));
        return new SupplierTotalsDto(
            count,
            sums.Sum(s => ToBase(s.Currency, s.Payable)),
            sums.Sum(s => ToBase(s.Currency, s.Advance)));
    }
}
