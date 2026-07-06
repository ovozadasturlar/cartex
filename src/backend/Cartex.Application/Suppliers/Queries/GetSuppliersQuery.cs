using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Suppliers.Queries;

public record GetSuppliersQuery : FilteringRequest, IRequest<IReadOnlyCollection<SupplierDto>>;

public record SupplierDto(long Id, string Name, string? Phone, decimal Payable);

public sealed class GetSuppliersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetSuppliersQuery, IReadOnlyCollection<SupplierDto>>
{
    public async Task<IReadOnlyCollection<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        return await db.Suppliers
            .ToPagedListAsync(request,
                s => new SupplierDto(s.Id, s.Name, s.Phone,
                    -(s.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => (decimal?)(a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault()))) ?? 0)),
                writer, cancellationToken);
    }
}
