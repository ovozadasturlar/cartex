using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Suppliers;

namespace Cartex.Application.Suppliers.Queries;

public record GetSuppliersQuery : FilteringRequest, IRequest<IReadOnlyCollection<SupplierDto>>;

public sealed class GetSuppliersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSuppliersQuery, IReadOnlyCollection<SupplierDto>>
{
    public async Task<IReadOnlyCollection<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var items = await db.Suppliers
            .ToPagedListAsync(request,
                s => new
                {
                    Dto = new SupplierDto(s.Id, s.Name, s.Phone, 0m, s.AcceptsReturns),
                    Payables = db.Accounts
                        .Where(a => a.SupplierId == s.Id && a.Type == AccountType.Debt && a.Balance != 0)
                        .Select(a => new CurrencyAmountDto(a.Currency, -a.Balance))
                        .ToList()
                },
                writer, cancellationToken);

        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);

        return items.Select(x => x.Dto with
        {
            Payable = x.Payables.Sum(p => p.Amount * (p.Currency == baseCode ? 1m : rates.GetValueOrDefault(p.Currency))),
            PayableBalances = x.Payables
        }).ToList();
    }
}
