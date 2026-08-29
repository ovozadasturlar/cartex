using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Shared.Models.Units;

namespace Cartex.Application.Units.Queries;

public record GetUnitsQuery : FilteringRequest, IRequest<IReadOnlyCollection<UnitDto>>;

public sealed class GetUnitsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetUnitsQuery, IReadOnlyCollection<UnitDto>>
{
    public async Task<IReadOnlyCollection<UnitDto>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SortBy))
        {
            request.SortBy = "Id";
            request.Descending = false;
        }
        return await db.Units
            .ToPagedListAsync(request,
                u => new UnitDto(u.Id, u.Name, u.ShortName, u.Dimension.ToString(), u.Factor, u.IsSystem, u.IsEnabled, u.IsDefault, u.AllowFractional, u.DefaultAllowAmountEntry),
                writer, cancellationToken);
    }
}
