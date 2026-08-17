using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Prepacks;

namespace Cartex.Application.Prepacks.Queries;

public record GetPrepacksQuery(long WarehouseId) : IRequest<IReadOnlyList<PrepackDto>>;

public sealed class GetPrepacksQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPrepacksQuery, IReadOnlyList<PrepackDto>>
{
    public async Task<IReadOnlyList<PrepackDto>> Handle(GetPrepacksQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await db.Prepacks
            .Where(p => p.WarehouseId == request.WarehouseId && p.Status == PrepackStatus.Active)
            .OrderByDescending(p => p.Id)
            .Take(200)
            .Select(p => new PrepackDto(
                p.Id,
                p.LabelCode,
                p.Variant.Product.Name,
                p.Variant.Product.Unit.ShortName,
                p.Quantity,
                Math.Round(p.UnitPrice * p.Quantity, 2),
                p.ExpiresAt != null && p.ExpiresAt <= now ? nameof(PrepackStatus.Expired) : p.Status.ToString(),
                p.ExpiresAt,
                p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
