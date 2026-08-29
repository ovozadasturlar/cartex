using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Stocks;

namespace Cartex.Application.Stocks.Queries;

public record GetLowStockQuery(long WarehouseId) : IRequest<IReadOnlyCollection<LowStockDto>>;

public sealed class GetLowStockQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLowStockQuery, IReadOnlyCollection<LowStockDto>>
{
    public async Task<IReadOnlyCollection<LowStockDto>> Handle(GetLowStockQuery request, CancellationToken cancellationToken)
    {
        var onHand = await db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId)
            .GroupBy(s => s.VariantId)
            .Select(g => new { VariantId = g.Key, OnHand = g.Sum(s => s.Quantity) })
            .ToListAsync(cancellationToken);

        if (onHand.Count == 0)
            return [];

        var variantIds = onHand.Select(o => o.VariantId).ToList();

        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new { v.Id, ProductName = v.Product.Name, UnitName = v.Product.Unit.Name, v.Product.MinStock })
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var warehouseName = await db.Warehouses
            .Where(w => w.Id == request.WarehouseId)
            .Select(w => w.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return onHand
            .Where(o => variants.TryGetValue(o.VariantId, out var v) && v.MinStock > 0 && o.OnHand < v.MinStock)
            .Select(o =>
            {
                var v = variants[o.VariantId];
                return new LowStockDto(o.VariantId, v.ProductName, v.UnitName, warehouseName, o.OnHand, v.MinStock);
            })
            .OrderBy(o => o.ProductName)
            .ToList();
    }
}
