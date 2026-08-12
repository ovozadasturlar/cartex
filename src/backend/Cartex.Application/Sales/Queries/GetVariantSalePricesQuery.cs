using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Sales.Queries;

/// <summary>
/// Prices a product was actually sold at, newest first. Feeds the editable price
/// picker used when returning goods that are not linked to a recorded sale.
/// </summary>
public sealed record GetVariantSalePricesQuery(long VariantId, long? CustomerId = null, int Take = 10)
    : IRequest<IReadOnlyCollection<VariantSalePriceDto>>;

public sealed class GetVariantSalePricesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IRequestHandler<GetVariantSalePricesQuery, IReadOnlyCollection<VariantSalePriceDto>>
{
    public async Task<IReadOnlyCollection<VariantSalePriceDto>> Handle(
        GetVariantSalePricesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(AppPermissions.Sales.View))
            throw new ForbiddenException("Savdo narxlarini ko'rishga ruxsat yo'q.");

        var query = db.SaleItems.AsNoTracking()
            .Where(x => x.VariantId == request.VariantId && x.Sale.Status != SaleStatus.Voided);
        if (!currentUser.CanAccessAllBranches)
            query = query.Where(x => currentUser.BranchIds.Contains(x.Sale.BranchId));
        if (request.CustomerId is { } customerId)
            query = query.Where(x => x.Sale.CustomerId == customerId);

        // A below-catalog price is stored as a header discount, so the price the customer
        // really paid is the line price scaled by the sale's net-to-gross ratio.
        var rows = await query
            .OrderByDescending(x => x.Id)
            .Take(Math.Clamp(request.Take, 1, 50) * 5)
            .Select(x => new
            {
                x.UnitPrice,
                x.Sale.TotalAmount,
                x.Sale.DiscountAmount,
                x.Sale.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x =>
            {
                var gross = x.TotalAmount + x.DiscountAmount;
                var netPrice = gross > 0
                    ? Math.Round(x.UnitPrice * x.TotalAmount / gross, 2)
                    : x.UnitPrice;
                return new VariantSalePriceDto(netPrice, x.CreatedAt);
            })
            .GroupBy(x => x.UnitPrice)
            .Select(x => new VariantSalePriceDto(x.Key, x.Max(row => row.LastSoldAt)))
            .OrderByDescending(x => x.LastSoldAt)
            .Take(Math.Clamp(request.Take, 1, 50))
            .ToList();
    }
}
