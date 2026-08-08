using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

using Cartex.Shared.Models.Ordering;

namespace Cartex.Application.Ordering.Queries;

public record GetCartsQuery(string? Status = null, long? WarehouseId = null, string? Kind = null) : IRequest<IReadOnlyCollection<CartListDto>>;

public sealed class GetCartsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCartsQuery, IReadOnlyCollection<CartListDto>>
{
    public async Task<IReadOnlyCollection<CartListDto>> Handle(GetCartsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Carts.AsQueryable();
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<CartStatus>(request.Status, true, out var status))
            query = query.Where(c => c.Status == status);
        if (!string.IsNullOrEmpty(request.Kind) && Enum.TryParse<CartKind>(request.Kind, true, out var kind))
            query = query.Where(c => c.Kind == kind);
        if (request.WarehouseId is not null)
            query = query.Where(c => c.WarehouseId == request.WarehouseId);
        if (!currentUser.HasPermission(AppPermissions.Sales.View) && !currentUser.HasPermission(AppPermissions.Sales.ViewAll))
            query = query.Where(c => c.CreatedBy == currentUser.UserId);

        var carts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(200)
            .Select(c => new
            {
                c.Id,
                c.AggregateCode,
                Status = c.Status.ToString(),
                CustomerName = c.Customer != null ? c.Customer.FullName : null,
                WarehouseName = c.Warehouse.Name,
                c.WarehouseId,
                ItemCount = c.Items.Count,
                c.CreatedAt,
                CreatedByName = db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                c.Note,
                Items = c.Items.Select(i => new { i.VariantId, i.Quantity }).ToList()
            })
            .ToListAsync(cancellationToken);

        if (carts.Count == 0)
            return [];

        var baseCurrency = await db.Businesses
            .Select(b => b.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var rates = await db.ExchangeRates
            .OrderByDescending(r => r.EffectiveAt)
            .GroupBy(r => r.Code)
            .Select(g => g.First())
            .ToDictionaryAsync(r => r.Code, r => r.Rate, cancellationToken);

        var allVariantIds = carts.SelectMany(c => c.Items.Select(i => i.VariantId)).Distinct().ToList();

        var prices = await db.ProductPrices
            .Where(pp => allVariantIds.Contains(pp.VariantId))
            .ToListAsync(cancellationToken);

        decimal ConvertPrice(decimal price, string? currency)
        {
            var rate = (currency == baseCurrency || string.IsNullOrEmpty(currency))
                ? 1m
                : (rates.TryGetValue(currency, out var r) ? r : 1m);
            return Math.Round(price * rate, 2);
        }

        return carts.Select(c =>
        {
            var estTotal = c.Items.Sum(i =>
            {
                var p = prices.FirstOrDefault(pp => pp.VariantId == i.VariantId && pp.WarehouseId == c.WarehouseId)
                     ?? prices.FirstOrDefault(pp => pp.VariantId == i.VariantId && pp.WarehouseId == null);
                var unitPrice = p is null ? 0 : ConvertPrice(p.SellingPrice, p.Currency);
                return i.Quantity * unitPrice;
            });

            return new CartListDto(
                c.Id,
                c.AggregateCode,
                c.Status,
                c.CustomerName,
                c.WarehouseName,
                c.ItemCount,
                c.CreatedAt,
                c.CreatedByName,
                c.Note,
                estTotal);
        }).ToList();
    }
}
