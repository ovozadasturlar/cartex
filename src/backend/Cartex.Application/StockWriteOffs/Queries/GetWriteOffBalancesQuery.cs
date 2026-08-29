using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockWriteOffs.Queries;

public sealed record GetWriteOffBalancesQuery(long? WarehouseId = null, long? VariantId = null)
    : IRequest<IReadOnlyList<WriteOffBalanceDto>>;

public sealed class GetWriteOffBalancesQueryHandler(
    IApplicationDbContext db,
    ISettingsService settings) : IRequestHandler<GetWriteOffBalancesQuery, IReadOnlyList<WriteOffBalanceDto>>
{
    public async Task<IReadOnlyList<WriteOffBalanceDto>> Handle(GetWriteOffBalancesQuery request, CancellationToken cancellationToken)
    {
        await WriteOffAccess.EnsureTrackedAsync(settings, cancellationToken);

        var rows = await db.InventoryMovements.AsNoTracking()
            .Where(x => (request.WarehouseId == null || x.WarehouseId == request.WarehouseId)
                && (request.VariantId == null || x.VariantId == request.VariantId)
                && (x.ToLocationKind == InventoryLocationKind.Scrap
                    || x.FromLocationKind == InventoryLocationKind.Scrap
                    || x.ToLocationKind == InventoryLocationKind.SupplierClaim
                    || x.FromLocationKind == InventoryLocationKind.SupplierClaim))
            .GroupBy(x => new
            {
                x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                Location = x.ToLocationKind == InventoryLocationKind.Scrap || x.FromLocationKind == InventoryLocationKind.Scrap
                    ? InventoryLocationKind.Scrap
                    : InventoryLocationKind.SupplierClaim,
                x.VariantId,
                ProductName = x.Variant.Product.Name
            })
            .Select(g => new
            {
                g.Key.WarehouseId,
                g.Key.WarehouseName,
                g.Key.Location,
                g.Key.VariantId,
                g.Key.ProductName,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Where(x => x.Quantity != 0)
            .OrderBy(x => x.WarehouseId).ThenBy(x => x.Location).ThenBy(x => x.ProductName)
            .Select(x => new WriteOffBalanceDto(
                x.WarehouseId, x.WarehouseName, x.Location.ToString(), x.VariantId, x.ProductName, -x.Quantity))];
    }
}

public sealed class GetWriteOffBalancesQueryValidator : AbstractValidator<GetWriteOffBalancesQuery>
{
    public GetWriteOffBalancesQueryValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).When(x => x.WarehouseId is not null);
        RuleFor(x => x.VariantId).GreaterThan(0).When(x => x.VariantId is not null);
    }
}
