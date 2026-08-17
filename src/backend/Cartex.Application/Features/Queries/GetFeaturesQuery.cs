using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Features;

namespace Cartex.Application.Features.Queries;

public record GetFeaturesQuery : IRequest<IReadOnlyList<FeatureDto>>;

public sealed class GetFeaturesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFeaturesQuery, IReadOnlyList<FeatureDto>>
{
    public async Task<IReadOnlyList<FeatureDto>> Handle(GetFeaturesQuery request, CancellationToken cancellationToken) =>
        await db.Features.OrderBy(f => f.Name)
            .Select(f => new FeatureDto(f.Code, f.Name, f.IsEnabled))
            .ToListAsync(cancellationToken);
}
