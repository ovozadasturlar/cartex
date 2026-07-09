using Cartex.Application.Common.Messaging;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Queries;

public record CartListDto(long Id, string AggregateCode, string Status, string? CustomerName, string WarehouseName, int ItemCount, DateTime CreatedAt);

public record GetCartsQuery(string? Status = null, long? WarehouseId = null) : IRequest<IReadOnlyCollection<CartListDto>>;

public sealed class GetCartsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCartsQuery, IReadOnlyCollection<CartListDto>>
{
    public async Task<IReadOnlyCollection<CartListDto>> Handle(GetCartsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Carts.AsQueryable();
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<CartStatus>(request.Status, true, out var status))
            query = query.Where(c => c.Status == status);
        if (request.WarehouseId is not null)
            query = query.Where(c => c.WarehouseId == request.WarehouseId);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(200)
            .Select(c => new CartListDto(
                c.Id,
                c.AggregateCode,
                c.Status.ToString(),
                c.Customer != null ? c.Customer.FullName : null,
                c.Warehouse.Name,
                c.Items.Count,
                c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
