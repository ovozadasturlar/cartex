using Cartex.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Queries;

public record GetCartByCodeQuery(string Code) : IRequest<CartDto?>;

public record CartItemDto(long ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record CartDto(string AggregateCode, string Status, long WarehouseId, long? CustomerId, string? CustomerName, decimal Total, List<CartItemDto> Items);

public sealed class GetCartByCodeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCartByCodeQuery, CartDto?>
{
    public async Task<CartDto?> Handle(GetCartByCodeQuery request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts
            .Include(c => c.Customer)
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken);

        if (cart is null)
            return null;

        var productIds = cart.Items.Select(i => i.ProductId).ToList();

        var prices = await db.ProductPrices
            .Where(pp => productIds.Contains(pp.ProductId) && (pp.WarehouseId == cart.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        decimal PriceOf(long productId) =>
            (prices.FirstOrDefault(p => p.ProductId == productId && p.WarehouseId == cart.WarehouseId)
             ?? prices.FirstOrDefault(p => p.ProductId == productId && p.WarehouseId == null))?.SellingPrice ?? 0;

        var items = cart.Items.Select(i =>
        {
            var unitPrice = PriceOf(i.ProductId);
            return new CartItemDto(i.ProductId, i.Product.Name, i.Quantity, unitPrice, unitPrice * i.Quantity);
        }).ToList();

        return new CartDto(cart.AggregateCode, cart.Status.ToString(), cart.WarehouseId, cart.CustomerId,
            cart.Customer != null ? cart.Customer.FullName : null, items.Sum(i => i.LineTotal), items);
    }
}
