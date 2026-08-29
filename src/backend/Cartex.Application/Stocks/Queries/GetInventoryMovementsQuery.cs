using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Stocks.Queries;

public sealed record GetInventoryMovementsQuery(long VariantId, long? WarehouseId, int Page = 1, int PageSize = 50)
    : IRequest<IReadOnlyCollection<InventoryMovementDto>>;

public sealed class GetInventoryMovementsQueryValidator : AbstractValidator<GetInventoryMovementsQuery>
{
    public GetInventoryMovementsQueryValidator()
    {
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0).When(x => x.WarehouseId is not null);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingRequest.MaxPageSize);
    }
}

public sealed class GetInventoryMovementsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetInventoryMovementsQuery, IReadOnlyCollection<InventoryMovementDto>>
{
    public async Task<IReadOnlyCollection<InventoryMovementDto>> Handle(
        GetInventoryMovementsQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.InventoryMovements
            .Where(x => x.VariantId == request.VariantId
                && (request.WarehouseId == null || x.WarehouseId == request.WarehouseId));

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Id,
                x.OccurredAt,
                x.Kind,
                x.FromLocationKind,
                x.FromLocationId,
                x.ToLocationKind,
                x.ToLocationId,
                x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                x.Quantity,
                UserName = x.User == null ? null : x.User.FullName,
                x.SourceType,
                x.SourceId
            })
            .ToListAsync(cancellationToken);

        writer.Write(new PagedListMetadata(total, request.Page, request.PageSize,
            (int)Math.Ceiling((double)total / request.PageSize)));

        return [.. rows.Select(x => new InventoryMovementDto(
            x.Id,
            x.OccurredAt,
            x.Kind.ToString(),
            x.FromLocationKind.ToString(),
            x.FromLocationId,
            x.ToLocationKind.ToString(),
            x.ToLocationId,
            x.WarehouseId,
            x.WarehouseName,
            x.Quantity,
            x.UserName,
            x.SourceType,
            x.SourceId))];
    }
}
