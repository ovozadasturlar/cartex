using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public record GetStockOnHandQuery(long WarehouseId) : IRequest<IReadOnlyCollection<StockOnHandDto>>;

public record StockOnHandDto(long ProductId, string ProductName, string? CategoryName, string UnitName, decimal Quantity, decimal SellingPrice, DateOnly? NearestExpiry);

public sealed class GetStockOnHandQueryHandler(IApplicationDbContext db) : IRequestHandler<GetStockOnHandQuery, IReadOnlyCollection<StockOnHandDto>>
{
    public async Task<IReadOnlyCollection<StockOnHandDto>> Handle(GetStockOnHandQuery request, CancellationToken cancellationToken)
    {
        var onHand = await db.Stocks
            .Where(s => s.WarehouseId == request.WarehouseId)
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, OnHand = g.Sum(s => s.Quantity), NearestExpiry = g.Min(s => s.ExpiredAt) })
            .ToListAsync(cancellationToken);

        if (onHand.Count == 0)
            return [];

        var productIds = onHand.Select(o => o.ProductId).ToList();

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, CategoryName = p.Category != null ? p.Category.Name : null, UnitName = p.Unit.Name })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var prices = await db.ProductPrices
            .Where(pp => productIds.Contains(pp.ProductId) && (pp.WarehouseId == request.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        decimal PriceOf(long productId) =>
            (prices.FirstOrDefault(p => p.ProductId == productId && p.WarehouseId == request.WarehouseId)
             ?? prices.FirstOrDefault(p => p.ProductId == productId && p.WarehouseId == null))?.SellingPrice ?? 0;

        return onHand
            .Where(o => products.ContainsKey(o.ProductId))
            .Select(o =>
            {
                var product = products[o.ProductId];
                return new StockOnHandDto(o.ProductId, product.Name, product.CategoryName, product.UnitName, o.OnHand, PriceOf(o.ProductId), o.NearestExpiry);
            })
            .ToList();
    }
}
