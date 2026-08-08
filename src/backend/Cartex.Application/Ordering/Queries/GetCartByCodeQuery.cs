using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;

using Cartex.Shared.Models.Ordering;

namespace Cartex.Application.Ordering.Queries;

public record GetCartByCodeQuery(string Code) : IRequest<CartDto?>;

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

        var baseCurrency = await db.Businesses
            .Select(b => b.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var rates = await db.ExchangeRates
            .OrderByDescending(r => r.EffectiveAt)
            .GroupBy(r => r.Code)
            .Select(g => g.First())
            .ToDictionaryAsync(r => r.Code, r => r.Rate, cancellationToken);

        var variantIds = cart.Items.Select(i => i.VariantId).ToList();

        var prices = await db.ProductPrices
            .Where(pp => variantIds.Contains(pp.VariantId) && (pp.WarehouseId == cart.WarehouseId || pp.WarehouseId == null))
            .ToListAsync(cancellationToken);

        decimal PriceOf(long variantId)
        {
            var p = prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == cart.WarehouseId)
                 ?? prices.FirstOrDefault(p => p.VariantId == variantId && p.WarehouseId == null);
            if (p is null) return 0;
            var rate = (p.Currency == baseCurrency || string.IsNullOrEmpty(p.Currency))
                ? 1m
                : (rates.TryGetValue(p.Currency, out var r) ? r : 1m);
            return Math.Round(p.SellingPrice * rate, 2);
        }

        var items = cart.Items.Select(i =>
        {
            var unitPrice = PriceOf(i.VariantId);
            return new CartItemDto(i.VariantId, i.Variant.Product.Name, i.Quantity, unitPrice, unitPrice * i.Quantity);
        }).ToList();

        return new CartDto(cart.AggregateCode, cart.Status.ToString(), cart.WarehouseId, cart.CustomerId,
            cart.Customer != null ? cart.Customer.FullName : null, items.Sum(i => i.LineTotal), items, cart.Note,
            cart.PaidCash, cart.PaidCard, cart.PaidBonus);
    }
}
