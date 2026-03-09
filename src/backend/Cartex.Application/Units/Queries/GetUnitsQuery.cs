using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Units.Queries;

public record GetUnitsQuery : IRequest<List<UnitDto>>;

public record UnitDto(long Id, string Name, string ShortName);

public sealed class GetUnitsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetUnitsQuery, List<UnitDto>>
{
    public async Task<List<UnitDto>> Handle(GetUnitsQuery request, CancellationToken cancellationToken)
    {
        return await db.Units
            .Select(u => new UnitDto(u.Id, u.Name, u.ShortName))
            .ToListAsync(cancellationToken);
    }
}
