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
            .Include(c => c.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product).ThenInclude(p => p.Unit)
            .Include(c => c.Participants)
            .Include(c => c.Payments)
            .Include(c => c.ClaimedByUser)
            .FirstOrDefaultAsync(c => c.AggregateCode == request.Code, cancellationToken);

        if (cart is null)
            return null;

        if (!currentUser.HasPermission(AppPermissions.Sales.View)
            && !currentUser.HasPermission(AppPermissions.Sales.Checkout)
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
            var catalogPrice = PriceOf(i.VariantId);
            var unitPrice = i.UnitPriceOverride ?? catalogPrice;
            var allowsFractional = i.Variant.Product.FractionalOverride ?? i.Variant.Product.Unit.AllowFractional;
            return new CartItemDto(i.VariantId, i.Variant.Product.Name, i.Quantity, unitPrice, unitPrice * i.Quantity,
                i.Variant.Product.Unit.ShortName, allowsFractional,
                i.Variant.Product.ImageKey, catalogPrice);
        }).ToList();

        var actions = new List<string>();
        var isOwner = cart.CreatedBy == currentUser.UserId;
        var isClaimant = cart.ClaimedByUserId == currentUser.UserId;
        var canOverrideClaim = currentUser.HasPermission(AppPermissions.Sales.OverrideClaim);
        if (cart.Status == Cartex.Domain.Enums.CartStatus.Open)
        {
            if (currentUser.HasPermission(AppPermissions.Sales.Create) && (isOwner || currentUser.HasPermission(AppPermissions.Sales.ViewAll)))
                actions.Add("edit");
            if (currentUser.HasPermission(AppPermissions.Sales.Checkout)) actions.Add("claim");
            if (isOwner || currentUser.HasPermission(AppPermissions.Sales.Checkout)) actions.Add("cancel");
            actions.Add("showQr");
        }
        else if (cart.Status is Cartex.Domain.Enums.CartStatus.Confirmed or Cartex.Domain.Enums.CartStatus.Ready)
        {
            if (currentUser.HasPermission(AppPermissions.Sales.Checkout) && (isClaimant || canOverrideClaim))
                actions.Add("checkout");
            if (isClaimant || canOverrideClaim) actions.Add("cancel");
        }
        else if (cart.Status == Cartex.Domain.Enums.CartStatus.CheckedOut)
        {
            if (cart.SaleId.HasValue) actions.Add("openSale");
        }
        else if (cart.Status == Cartex.Domain.Enums.CartStatus.Cancelled
                 && (currentUser.HasPermission(AppPermissions.Sales.Create)
                     || currentUser.HasPermission(AppPermissions.Sales.Pick)))
            actions.Add("requeue");

        return new CartDto(cart.AggregateCode, cart.Status.ToString(), cart.WarehouseId, cart.CustomerId,
            cart.Customer != null ? cart.Customer.FullName : null, items.Sum(i => i.LineTotal), items, cart.Note,
            cart.PaidCash, cart.PaidCard, cart.PaidBonus,
            cart.Participants.OrderBy(x => x.Id).Select(x => new CartParticipantDto(
                x.RoleDefinitionId, x.PartyId, x.PartyNameSnapshot,
                x.PartyPhoneSnapshot, x.RoleLabelSnapshot)).ToList(),
            cart.Payments.OrderBy(x => x.Id).Select(x => new CartPaymentDto(
                x.Method.ToString(), x.Currency, x.Amount)).ToList(),
            cart.DebtCurrency, cart.DebtDueDate, cart.CreditAmount, cart.UseCustomerAdvance,
            cart.Id, cart.Kind.ToString(), cart.Version, cart.CreatedBy,
            cart.CreatedBy.HasValue
                ? await db.Users.Where(x => x.Id == cart.CreatedBy).Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken)
                : null,
            cart.ClaimedByUserId, cart.ClaimedByUser?.FullName, cart.ClaimedAt, cart.SaleId,
            cart.CancelledAt, cart.CancellationReason, cart.RequeuedFromCartId, actions);
    }
}
