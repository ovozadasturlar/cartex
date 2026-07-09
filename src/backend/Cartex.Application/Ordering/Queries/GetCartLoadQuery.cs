using Cartex.Application.Common.Messaging;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Queries;

public record CartLoadItemDto(long VariantId, string ProductName, string UnitName, decimal TotalQuantity);

public record GetCartLoadQuery(long? WarehouseId = null, string? Status = null) : IRequest<IReadOnlyCollection<CartLoadItemDto>>;

public sealed class GetCartLoadQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCartLoadQuery, IReadOnlyCollection<CartLoadItemDto>>
{
    public async Task<IReadOnlyCollection<CartLoadItemDto>> Handle(GetCartLoadQuery request, CancellationToken cancellationToken)
    {
        var statuses = ParseStatuses(request.Status);

        var query = db.Carts.Where(c => statuses.Contains(c.Status));
        if (request.WarehouseId is not null)
            query = query.Where(c => c.WarehouseId == request.WarehouseId);

        var rows = await query
            .SelectMany(c => c.Items)
            .GroupBy(i => new { i.VariantId, ProductName = i.Variant.Product.Name, UnitName = i.Variant.Product.Unit.Name })
            .Select(g => new { g.Key.VariantId, g.Key.ProductName, g.Key.UnitName, TotalQuantity = g.Sum(i => i.Quantity) })
            .OrderBy(x => x.ProductName)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CartLoadItemDto(r.VariantId, r.ProductName, r.UnitName, r.TotalQuantity)).ToList();
    }

    private static List<CartStatus> ParseStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return [CartStatus.Open, CartStatus.Confirmed];

        var parsed = status
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => Enum.TryParse<CartStatus>(s, true, out _))
            .Select(s => Enum.Parse<CartStatus>(s, true))
            .Distinct()
            .ToList();

        return parsed.Count > 0 ? parsed : [CartStatus.Open, CartStatus.Confirmed];
    }
}
