using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using MediatR;

namespace Cartex.Application.Customers.Queries;

public record GetCustomersQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerDto>>;

public record CustomerDto(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance);

public sealed class GetCustomersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomersQuery, IReadOnlyCollection<CustomerDto>>
{
    public async Task<IReadOnlyCollection<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        return await db.Customers
            .ToPagedListAsync(request,
                c => new CustomerDto(c.Id, c.FullName, c.Phone, c.CardBarcode, c.DiscountPct, c.CashbackBalance),
                writer, cancellationToken);
    }
}
