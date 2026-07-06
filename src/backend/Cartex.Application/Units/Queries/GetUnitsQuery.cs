using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;

namespace Cartex.Application.Units.Queries;

public record GetUnitsQuery : FilteringRequest, IRequest<IReadOnlyCollection<UnitDto>>;

public record UnitDto(long Id, string Name, string ShortName, string Dimension, decimal Factor, bool IsSystem);

public sealed class GetUnitsQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetUnitsQuery, IReadOnlyCollection<UnitDto>>
{
    public async Task<IReadOnlyCollection<UnitDto>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        return await db.Units
            .ToPagedListAsync(request,
                u => new UnitDto(u.Id, u.Name, u.ShortName, u.Dimension.ToString(), u.Factor, u.IsSystem),
                writer, cancellationToken);
    }
}
