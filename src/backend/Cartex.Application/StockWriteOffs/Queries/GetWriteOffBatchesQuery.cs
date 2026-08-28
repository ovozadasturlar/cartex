using Cartex.Application.Common.Interfaces;
using Cartex.Persistence;
using Cartex.Shared.Models.Stocks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.StockWriteOffs.Queries;

public sealed record GetWriteOffBatchesQuery(long WarehouseId, long VariantId)
    : IRequest<IReadOnlyList<WriteOffBatchDto>>;

public sealed class GetWriteOffBatchesQueryHandler(
    IApplicationDbContext db,
    ISettingsService settings) : IRequestHandler<GetWriteOffBatchesQuery, IReadOnlyList<WriteOffBatchDto>>
{
    public async Task<IReadOnlyList<WriteOffBatchDto>> Handle(GetWriteOffBatchesQuery request, CancellationToken cancellationToken)
    {
        await WriteOffAccess.EnsureTrackedAsync(settings, cancellationToken);

        return await db.Stocks.AsNoTracking()
            .Where(x => x.WarehouseId == request.WarehouseId && x.VariantId == request.VariantId
                && x.Quantity > 0 && !x.IsDeficit)
            .OrderBy(x => x.ExpiredAt == null).ThenBy(x => x.ExpiredAt).ThenBy(x => x.CreatedAt)
            .Select(x => new WriteOffBatchDto(
                x.Id,
                x.VariantId,
                x.Quantity,
                x.PurchasePrice,
                x.ExpiredAt,
                x.Supply == null ? null : x.Supply.SupplierId,
                x.Supply == null || x.Supply.Supplier == null ? null : x.Supply.Supplier.Name,
                x.Supply != null && x.Supply.Supplier != null && x.Supply.Supplier.AcceptsReturns))
            .ToListAsync(cancellationToken);
    }
}

public sealed class GetWriteOffBatchesQueryValidator : AbstractValidator<GetWriteOffBatchesQuery>
{
    public GetWriteOffBatchesQueryValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.VariantId).GreaterThan(0);
    }
}
