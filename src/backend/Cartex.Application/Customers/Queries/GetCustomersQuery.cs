using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Customers.Queries;

public record GetCustomersQuery(string? Search) : IRequest<List<CustomerDto>>;

public record CustomerDto(long Id, string FullName, string? Phone, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance);

public sealed class GetCustomersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCustomersQuery, List<CustomerDto>>
{
    public async Task<List<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsQueryable();

        if (request.Search is not null)
            query = query.Where(c => c.FullName.Contains(request.Search) || (c.Phone != null && c.Phone.Contains(request.Search)));

        return await query
            .Select(c => new CustomerDto(c.Id, c.FullName, c.Phone, c.CardBarcode, c.DiscountPct, c.CashbackBalance))
            .ToListAsync(cancellationToken);
    }
}
