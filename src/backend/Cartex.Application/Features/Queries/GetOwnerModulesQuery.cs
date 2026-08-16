using Cartex.Domain.Authorization;
using Cartex.Persistence;
using Cartex.Shared.Models.Features;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Features.Queries;

public sealed record GetOwnerModulesQuery : IRequest<IReadOnlyList<OwnerModuleDto>>;

public sealed class GetOwnerModulesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOwnerModulesQuery, IReadOnlyList<OwnerModuleDto>>
{
    public async Task<IReadOnlyList<OwnerModuleDto>> Handle(GetOwnerModulesQuery request, CancellationToken cancellationToken) =>
        await db.Features
            .Where(f => FeatureCatalog.ConfigurableCodes.Contains(f.Code))
            .OrderBy(f => f.Name)
            .Select(f => new OwnerModuleDto(f.Code, f.Name, f.IsEnabled, f.OwnerEnabled))
            .ToListAsync(cancellationToken);
}
