using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Queries;

public record FeatureDto(string Code, string Name, bool IsEnabled);

public record GetFeaturesQuery : IRequest<IReadOnlyList<FeatureDto>>;

public sealed class GetFeaturesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFeaturesQuery, IReadOnlyList<FeatureDto>>
{
    public async Task<IReadOnlyList<FeatureDto>> Handle(GetFeaturesQuery request, CancellationToken cancellationToken) =>
        await db.Features.OrderBy(f => f.Name)
            .Select(f => new FeatureDto(f.Code, f.Name, f.IsEnabled))
            .ToListAsync(cancellationToken);
}
