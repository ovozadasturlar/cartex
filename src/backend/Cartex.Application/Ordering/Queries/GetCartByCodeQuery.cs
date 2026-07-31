using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Queries;

public record GetCartByCodeQuery(string Code) : IRequest<CartDto?>;

public record CartItemDto(long VariantId, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record CartDto(string AggregateCode, string Status, long WarehouseId, long? CustomerId, string? CustomerName, decimal Total, List<CartItemDto> Items, string? Note);

public sealed class GetCartByCodeQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCartByCodeQuery, CartDto?>
{
    public async Task<CartDto?> Handle(GetCartByCodeQuery request, CancellationToken cancellationToken)
    {
        var cart = await db.Carts
            .Include(c => c.Customer)
            .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken);

        if (cart is null)
            return null;

        if (!currentUser.HasPermission(AppPermissions.Sales.Checkout)
            && !currentUser.HasPermission(AppPermissions.Sales.Create)
            && cart.CreatedBy != currentUser.UserId)
            return null;

        var variantIds = cart.Items.Select(i => i.VariantId).ToList();

        var prices = await db.ProductPrices
            .Where(pp => variantIds.Contains(pp.VariantId) && (pp.WarehouseId == cart.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        decimal PriceOf(long variantId) =>
            (prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == cart.WarehouseId)
             ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null))?.SellingPrice ?? 0;

        var items = cart.Items.Select(i =>
        {
            var unitPrice = PriceOf(i.VariantId);
            return new CartItemDto(i.VariantId, i.Variant.Product.Name, i.Quantity, unitPrice, unitPrice * i.Quantity);
        }).ToList();

        return new CartDto(cart.AggregateCode, cart.Status.ToString(), cart.WarehouseId, cart.CustomerId,
            cart.Customer != null ? cart.Customer.FullName : null, items.Sum(i => i.LineTotal), items, cart.Note);
    }
}
