using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreOrderDto(string Code, string Status, DateTime CreatedAt, int ItemCount, string Warehouse);

public record GetStoreOrdersQuery : IRequest<IReadOnlyList<StoreOrderDto>>;

public sealed class GetStoreOrdersQueryHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<GetStoreOrdersQuery, IReadOnlyList<StoreOrderDto>>
{
    public async Task<IReadOnlyList<StoreOrderDto>> Handle(GetStoreOrdersQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        return await db.Carts
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.Id)
            .Take(100)
            .Select(c => new StoreOrderDto(
                c.AggregateCode,
                c.Status.ToString(),
                c.CreatedAt,
                c.Items.Count,
                c.Warehouse.Name))
            .ToListAsync(cancellationToken);
    }
}
