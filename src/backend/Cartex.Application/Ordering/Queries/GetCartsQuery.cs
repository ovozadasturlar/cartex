using Cartex.Application.Common.Messaging;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Ordering.Queries;

public record CartListDto(long Id, string AggregateCode, string Status, string? CustomerName, string WarehouseName, int ItemCount, DateTime CreatedAt, string? CreatedByName, string? Note, decimal EstimatedTotal);

public record GetCartsQuery(string? Status = null, long? WarehouseId = null) : IRequest<IReadOnlyCollection<CartListDto>>;

public sealed class GetCartsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCartsQuery, IReadOnlyCollection<CartListDto>>
{
    public async Task<IReadOnlyCollection<CartListDto>> Handle(GetCartsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Carts.AsQueryable();
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<CartStatus>(request.Status, true, out var status))
            query = query.Where(c => c.Status == status);
        if (request.WarehouseId is not null)
            query = query.Where(c => c.WarehouseId == request.WarehouseId);
        if (!currentUser.HasPermission(AppPermissions.Sales.View) && !currentUser.HasPermission(AppPermissions.Sales.ViewAll))
            query = query.Where(c => c.CreatedBy == currentUser.UserId);

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
                c.CreatedAt,
                db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                c.Note,
                c.Items.Sum(i => i.Quantity * db.ProductPrices
                    .Where(pp => pp.VariantId == i.VariantId && (pp.WarehouseId == c.WarehouseId || pp.WarehouseId == null))
                    .OrderBy(pp => pp.WarehouseId == null)
                    .Select(pp => pp.SellingPrice)
                    .FirstOrDefault())))
            .ToListAsync(cancellationToken);
    }
}
